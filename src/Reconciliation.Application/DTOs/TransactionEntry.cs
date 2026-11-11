namespace Reconciliation.Application.DTOs;

/// <summary>
/// A single transaction category with count and amount.
/// Maps to the # / Amount paired columns in the Excel report (Section D).
/// </summary>
public record TransactionEntry(int Count, decimal Amount);
