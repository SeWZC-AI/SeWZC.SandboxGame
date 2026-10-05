// Opt in only focused, bounded checks of a command, query or rule. Whole-world
// evolution and cross-system save/resume scenarios remain integration tests.

[AttributeUsage(AttributeTargets.Method)]
internal sealed class UnitTestAttribute : Attribute;

// Keep expensive multi-seed/large-world regressions available explicitly.
[AttributeUsage(AttributeTargets.Method)]
internal sealed class LongRunningTestAttribute : Attribute;
