import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile


spec = importlib.util.spec_from_file_location("cashbricks_release", Path(__file__).resolve().parents[1] / "scripts/release.py")
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class ReleaseValidationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="cashbricks-release-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.root_patch = patch.object(release, "ROOT", self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)
        (self.root / "src").mkdir()
        (self.root / "releases").mkdir()
        (self.root / "dist").mkdir()
        (self.root / "docs").mkdir()
        (self.root / "docs/installation.md").write_text("Test installation guide.\n")
        (self.root / "CHANGELOG.md").write_text("Test changelog.\n")
        (self.root / "CashBricks.csproj").write_text("<Project><PropertyGroup><Version>1.2.3</Version></PropertyGroup></Project>")
        (self.root / "src/Main.cs").write_text('[assembly: MelonInfo(typeof(CashBricks.Main), "Cash Bricks", "1.2.3", "Astra")]')
        (self.root / "releases/v1.2.3.md").write_text("Test release notes.\n")

    def package(self, extra=False, corrupt=False):
        files = {"README.md": b"install instructions", "Mods/CashBricks.dll": b"test mod binary",
                 "CHANGELOG.md": (self.root / "CHANGELOG.md").read_bytes(),
                 "docs/installation.md": (self.root / "docs/installation.md").read_bytes()}
        for name in ("cashbrick.obj", "cashbrick_packed.obj", "cashbrick_albedo.png", "cashbrick_icon.png",
                     "cashbrick_wrap.obj", "cashbrick_wrap_albedo.png", "cashbrick_wrapped_icon.png"):
            files[f"UserData/CashBricks/{name}"] = name.encode()
        if extra:
            files["MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll"] = b"must not ship"
        manifest = "".join(f"{hashlib.sha256(data).hexdigest()}  {name}\n" for name, data in files.items())
        if corrupt:
            files["Mods/CashBricks.dll"] = b"tampered binary"
        archive = self.root / "dist/CashBricks-1.2.3.zip"
        with zipfile.ZipFile(archive, "w") as bundle:
            for name, data in files.items():
                bundle.writestr(name, data)
            bundle.writestr("SHA256SUMS.txt", manifest)
        return archive

    def test_consistent_version_and_tag(self):
        self.assertEqual(release.release_info("v1.2.3")[0], "1.2.3")

    def test_wrong_tag_is_rejected(self):
        with self.assertRaises(ValueError):
            release.release_info("v1.2.4")

    def test_melon_version_must_match(self):
        (self.root / "src/Main.cs").write_text('[assembly: MelonInfo(typeof(CashBricks.Main), "Cash Bricks", "1.2.2", "Astra")]')
        with self.assertRaises(ValueError):
            release.release_info()

    def test_release_notes_required(self):
        (self.root / "releases/v1.2.3.md").unlink()
        with self.assertRaises(ValueError):
            release.release_info()

    def test_expected_package_is_accepted(self):
        release.verify_package(self.package())

    def test_game_assemblies_cannot_enter_release(self):
        with self.assertRaises(ValueError):
            release.verify_package(self.package(extra=True))

    def test_modified_payload_is_rejected(self):
        with self.assertRaises(ValueError):
            release.verify_package(self.package(corrupt=True))

    def test_auth_failure_stops_before_build_or_push(self):
        with patch.object(sys, "argv", ["release.py"]), \
             patch.object(release, "github_cli", return_value="gh"), \
             patch.object(release, "repository", return_value=("owner/repo", "main", "a" * 40)), \
             patch.object(release, "authenticate", side_effect=ValueError("bad credentials")), \
             patch.object(release, "run") as run:
            with self.assertRaises(ValueError):
                release.main()
            run.assert_not_called()

    def publish(self, digest_ok):
        archive = self.package()
        commit = "a" * 40

        def fake_run(*args, **kwargs):
            if args[:3] == ("gh", "release", "view"):
                return '{"databaseId":123}'
            if args[:3] == ("gh", "api", "repos/owner/repo/releases/123"):
                paths = (archive, archive.with_suffix(".zip.sha256"))
                assets = [{"name": path.name, "size": path.stat().st_size,
                           "digest": "sha256:" + (hashlib.sha256(path.read_bytes()).hexdigest() if digest_ok else "0" * 64)}
                          for path in paths]
                return json.dumps({"draft": True, "assets": assets})
            return ""

        def fake_process(args, **kwargs):
            if args[:3] == ("gh", "release", "view"):
                return subprocess.CompletedProcess(args, 0, '{"isDraft":true,"url":"https://github.com/owner/repo/releases/tag/v1.2.3"}', "")
            if args[:3] == ("git", "rev-parse", "--verify"):
                return subprocess.CompletedProcess(args, 0, commit + "\n", "")
            raise AssertionError(args)

        with patch.object(sys, "argv", ["release.py", "--offline"]), \
             patch.object(release, "github_cli", return_value="gh"), \
             patch.object(release, "repository", return_value=("owner/repo", "main", commit)), \
             patch.object(release, "authenticate"), \
             patch.object(release.subprocess, "run", side_effect=fake_process), \
             patch.object(release, "run", side_effect=fake_run) as run:
            if digest_ok:
                with patch("builtins.print"):
                    release.main()
            else:
                with self.assertRaises(ValueError):
                    release.main()
            calls = [call.args for call in run.call_args_list]
        return calls

    def test_bad_remote_digest_leaves_draft_unpublished(self):
        calls = self.publish(False)
        self.assertFalse(any(args[:3] == ("gh", "release", "edit") for args in calls))

    def test_matching_assets_publish_verified_draft(self):
        calls = self.publish(True)
        self.assertTrue(any(args[:3] == ("gh", "release", "edit") and "--draft=false" in args for args in calls))
        self.assertTrue(any(args[0] == "git" and "--atomic" in args and "push" in args for args in calls))
        self.assertTrue(any(args[:3] == ("gh", "api", "repos/owner/repo/releases/123") for args in calls))
        self.assertFalse(any(args[:2] == ("gh", "api") and "/releases/tags/" in args[2] for args in calls))


if __name__ == "__main__":
    unittest.main()
