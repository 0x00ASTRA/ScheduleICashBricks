using CashBricks;

int assertions = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    assertions++;
}
foreach (int limit in new[] { 1, 10, 20, 50 })
foreach (int occupied in Enumerable.Range(0, limit + 1))
foreach (double first in new[] { 0, 1, 499.99, 600.25, 999.99, 1000 })
foreach (double second in new[] { 0, 400, 400.50, 999.99, 1000 })
{
    int capacity = limit - occupied;
    int expectedCount = CashRules.BricksToPress(first + second, capacity);
    double debit = expectedCount * CashRules.DollarsPerBrick;
    double taken1 = CashRules.TakeFromSlot(first, debit);
    double taken2 = CashRules.TakeFromSlot(second, debit - taken1);
    Check(expectedCount >= 0 && expectedCount <= capacity, "Never exceed the live product brick stack limit");
    Check(taken1 <= first && taken2 <= second, "Never overdraw an input");
    Check(Math.Abs(debit - taken1 - taken2) < 1e-8, "Every created brick is backed by exactly $1,000");
    Check(Math.Abs((first + second) - (first - taken1 + second - taken2 + debit)) < 1e-8, "Conserve cash across all slot combinations");
}
foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1.0 })
    Check(CashRules.BricksToPress(invalid, 20) == 0, "Invalid cash cannot mint bricks");

long Cents(float value) => (long)Math.Round((double)value * 100, MidpointRounding.AwayFromZero);
Check(CashRules.TryPlanPress(new[] { 600.10f, 399.90f }, 20, out int count, out var remainders) &&
      count == 1 && remainders.All(balance => balance == 0),
    "Two decimal cash stacks totalling $1,000 are accepted despite native float noise");
Check(CashRules.TryPlanPress(new[] { 0.01f, 1000f }, 20, out count, out remainders) &&
      count == 1 && remainders[0] == 0.01f && remainders[1] == 0,
    "Preserve a tiny untouched stack by consuming the full stack first");
Check(!CashRules.TryPlanPress(new[] { 1_000_000_000f }, 1, out count, out remainders) &&
      count == 0 && remainders.Length == 0,
    "Refuse a large cash remainder that would round by $24");
Check(CashRules.TryPlanPress(new[] { 1_000_000_000f, 1_000_000_000f, 1_000_000_000f },
      int.MaxValue, out count, out remainders) && count == 3_000_000 && remainders.All(balance => balance == 0),
    "Large plans do not overflow an integer dollar debit");
foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, float.MaxValue })
{
    var balances = new[] { invalid, 1000f };
    Check(!CashRules.TryPlanPress(balances, 20, out count, out remainders) && count == 0 && remainders.Length == 0,
        "Reject the entire plan if any cash slot is invalid");
    Check(!CashRules.TryPlanUnpack(invalid, 1_000_000_000f, out _), "Invalid cash outputs cannot receive unpacked money");
}
foreach (int capacity in new[] { -1, 0 })
    Check(!CashRules.TryPlanPress(new[] { 1000f, 1000f }, capacity, out _, out _),
        "No output capacity means no staged cash changes");
Check(!CashRules.TryPlanPress(Array.Empty<float>(), 20, out _, out _), "Empty inputs cannot create bricks");
Check(!CashRules.TryPlanPress(new[] { 999.99f, 0f }, 20, out _, out _), "One cent short remains insufficient");

var random = new Random(1042026);
for (int sample = 0; sample < 25_000; sample++)
{
    var balances = new[] { random.Next(0, 100_001) / 100f, random.Next(0, 100_001) / 100f };
    var snapshot = balances.ToArray();
    int capacity = random.Next(0, 4);
    int expected = (int)Math.Min(balances.Sum(Cents) / 100_000, capacity);
    bool planned = CashRules.TryPlanPress(balances, capacity, out count, out remainders);
    Check(planned == (expected > 0) && count == expected, "Every ordinary cent balance produces the expected count");
    Check(balances.SequenceEqual(snapshot), "Planning never mutates input balances");
    if (!planned) continue;
    Check(balances.Sum(Cents) == remainders.Sum(Cents) + count * 100_000L, "Native float plans conserve every cent");
    Check(remainders.Zip(balances).All(pair => pair.First >= 0 && Cents(pair.First) <= Cents(pair.Second)),
        "Every native cash remainder is nonnegative and never increases");
}
foreach (float cashOutput in new[] { 0f, 0.01f, 123.45f, 600.10f, 10_000f, 999_000f })
    Check(CashRules.TryPlanUnpack(cashOutput, 1_000_000_000f, out float after) && Cents(after) - Cents(cashOutput) == 100_000,
        "Unpacking adds exactly $1,000 to the loose cash output to the cent");
Check(CashRules.TryPlanUnpack(0f, 1000f, out _), "Allow unpacking exactly to the cash output cap");
Check(!CashRules.TryPlanUnpack(0.01f, 1000f, out _), "Reject unpacking one cent past the cash output cap");
Check(!CashRules.TryPlanUnpack(134_217_728f, 1_000_000_000f, out _), "Reject an output credit that would round to $992");
Check(!CashRules.TryPlanUnpack(1_000_000_000f, 1_000_000_000f, out _), "Reject a full cash output");
Check(!CashRules.TryPlanUnpack(0f, float.NaN, out _), "Reject an invalid cash output cap");
foreach (float initialCash in new[] { 0f, 0.01f, 123.45f, 600.10f })
foreach (int brickCount in new[] { 1, 2, 20, 40 })
{
    float cashOutput = initialCash;
    for (int i = 0; i < brickCount; i++)
    {
        Check(CashRules.TryPlanUnpack(cashOutput, 1_000_000_000f, out cashOutput), "Every supported station output can accept another unpacked brick");
        Check(Cents(cashOutput) == Cents(initialCash) + (i + 1) * 100_000L, "Repeated station unpacking preserves every cent");
    }
}

for (int failingStep = 0; failingStep < 3; failingStep++)
{
    var state = new[] { 650, 500, 0 };
    var snapshot = state.ToArray();
    var restored = new List<int>();
    var failure = new InvalidOperationException("Native write failed after mutation");
    try
    {
        CashTransaction.Run(state.Length, i =>
        {
            state[i] = 0;
            if (i == failingStep) throw failure;
        }, i => { restored.Add(i); state[i] = snapshot[i]; });
        Check(false, "A failed native write must propagate its exception");
    }
    catch (InvalidOperationException caught) { Check(ReferenceEquals(caught, failure), "Keep the original native exception"); }
    Check(state.SequenceEqual(snapshot), "Restore inputs and output after a partial native write");
    Check(restored.SequenceEqual(Enumerable.Range(0, failingStep + 1).Reverse()),
        "Restore the failing write and every previous write in reverse order");
}
var rollbackAttempts = new List<int>();
try
{
    CashTransaction.Run(3, i => { if (i == 2) throw new InvalidOperationException("write failure"); }, i =>
    {
        rollbackAttempts.Add(i);
        if (i == 1) throw new IOException("rollback failure");
    });
    Check(false, "Report failed restoration");
}
catch (AggregateException failure)
{
    Check(failure.InnerExceptions.Count == 2, "Report both the native write failure and restoration failure");
}
Check(rollbackAttempts.SequenceEqual(new[] { 2, 1, 0 }), "A failed restoration never prevents restoring other slots");
int applied = 0;
CashTransaction.Run(3, _ => applied++, _ => Check(false, "A successful transaction does not restore snapshots"));
Check(applied == 3, "Apply every step in a successful transaction");

// Exercise every six-event interaction, including premature and repeated
// callbacks, partial pouring, cancelled pours, and cancellation after success.
for (int sequence = 0; sequence < 46_656; sequence++)
{
    var flow = new CashPressFlow();
    int events = sequence, stage = 0, bricks = 0, commits = 0;
    float balance = 2000;
    for (int step = 0; step < 6; step++, events /= 6)
    {
        int action = events % 6;
        switch (action)
        {
            case 0: flow.LoadMould(19); break;
            case 1:
                flow.LoadMould(20);
                if (stage == 0) stage = 1;
                break;
            case 2: flow.PullHandle(false); break;
            case 3:
                flow.PullHandle(true);
                if (stage == 1) stage = 2;
                break;
            case 4:
                flow.Commit(() =>
                {
                    Check(stage == 2, "Cash changes require a fully loaded mould followed by a completed handle pull");
                    Check(CashRules.TryPlanPress(new[] { balance }, 1, out int made, out var left), "One physical cycle has a valid cash plan");
                    balance = left[0]; bricks += made; commits++;
                });
                if (stage == 2) stage = 3;
                break;
            case 5:
                flow.Cancel();
                if (stage != 3) stage = -1;
                break;
        }
        Check(bricks == (stage == 3 ? 1 : 0) && commits == bricks,
            "Premature completion, cancellation, and repeated callbacks cannot mint a cash brick");
        Check(balance + bricks * 1000 == 2000, "Physical task callbacks conserve money after every interaction");
    }
}
var reentered = new CashPressFlow();
reentered.LoadMould(20);
reentered.PullHandle(true);
Check(reentered.Commit(() => Check(!reentered.Commit(() => throw new Exception("Double mint")),
    "Native callbacks cannot re-enter an in-progress cash commit")), "Valid physical task commits once");
var broken = new CashPressFlow();
broken.LoadMould(20);
broken.PullHandle(true);
try { broken.Commit(() => throw new IOException("Native store failed")); Check(false, "Commit failure must propagate"); }
catch (IOException) { Check(broken.State == CashPressFlow.Phase.Cancelled, "A failed physical commit ends the session"); }
Check(!broken.Commit(() => throw new Exception("Retry mint")), "Failed physical callbacks cannot retry the transaction");
// Native NPC work is separate from the player's pouring/handle flow. Explore
// offers, completion and cancellation in every order, including late callbacks.
for (int sequence = 0; sequence < 729; sequence++)
{
    var cycle = new CashEmployeeCycle();
    bool offered = false, cancelled = false, completed = false;
    int events = sequence, bricks = 0;
    float cash = 2000;
    for (int step = 0; step < 6; step++, events /= 3)
    {
        switch (events % 3)
        {
            case 0:
                bool canOffer = !cancelled && !completed;
                Check(cycle.Offer() == canOffer, "Only a live employee cycle can offer its completion token");
                offered |= canOffer;
                break;
            case 1:
                bool canCommit = offered && !cancelled && !completed;
                Check(cycle.Commit(() => { cash -= 1000; bricks++; }) == canCommit,
                    "An employee completion requires an offered token and can consume it only once");
                completed |= canCommit;
                break;
            case 2:
                cycle.Cancel();
                if (!completed) cancelled = true;
                break;
        }
        Check(bricks == (completed ? 1 : 0) && cash + bricks * 1000 == 2000,
            "Every employee callback order conserves cash and produces at most one brick");
    }
}
var employeeReentry = new CashEmployeeCycle();
employeeReentry.Offer();
Check(employeeReentry.Commit(() =>
{
    Check(!employeeReentry.Offer() && !employeeReentry.Commit(() => throw new Exception("Double employee mint")),
        "Native inventory callbacks cannot reenter an employee completion");
    employeeReentry.Cancel();
}), "An in-progress employee commit finishes once even if a callback cancels the session");
var employeeFailure = new CashEmployeeCycle();
employeeFailure.Offer();
var employeeMoney = new[] { 1500, 0 };
try
{
    employeeFailure.Commit(() => CashTransaction.Run(2, i =>
    {
        employeeMoney[i] += i == 0 ? -1000 : 1000;
        if (i == 1) throw new IOException("Employee output failed after mutation");
    }, i => employeeMoney[i] = i == 0 ? 1500 : 0));
    Check(false, "A failed employee write must propagate its transaction error");
}
catch (IOException)
{
    Check(employeeMoney.SequenceEqual(new[] { 1500, 0 }) && employeeFailure.State == CashEmployeeCycle.Phase.Cancelled,
        "An employee output failure restores cash and cancels its completion token");
}
Check(!employeeFailure.Offer() && !employeeFailure.Commit(() => throw new Exception("Late employee mint")),
    "A failed employee cycle cannot mint from a later or repeated callback");
Console.WriteLine($"PASS: {assertions} cash conservation, precision, capacity, transaction, physical task, and employee cycle assertions");
