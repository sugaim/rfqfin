using Rfq.Domain;
using Xunit;

namespace Rfq.Domain.Tests;

public sealed class SemanticDomainTests
{
    private static readonly UserId Sales = UserId.Create("sales");
    private static readonly UserId Trader = UserId.Create("trader");
    private static readonly DateOnly Today = new(2026, 9, 21);
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StateVersion_validates_and_increments_checked()
    {
        Assert.Throws<DomainValidationException>(() => new StateVersion(0));
        Assert.Equal(2, new StateVersion(1).Next().Value);
        Assert.Throws<DomainValidationException>(() => new StateVersion(long.MaxValue).Next());
    }

    [Fact]
    public void DeskId_normalizes_and_rejects_empty_values()
    {
        Assert.Equal("jpy-credit", DeskId.Create("  jpy-credit  ").Value);
        Assert.Throws<DomainValidationException>(() => DeskId.Create(" "));
    }

    [Fact]
    public void Present_and_unpresent_preserve_confirmed_quote()
    {
        var (rfq, quote) = ConfirmQuote();
        var presented = RfqLifecycleTransitions.Present(rfq, rfq.Version);
        Assert.IsType<PresentedRfq>(presented.Lifecycle);
        Assert.Equal(quote.QuoteId, presented.CurrentQuoteId);

        var active = RfqLifecycleTransitions.Unpresent(presented, presented.Version);
        Assert.IsType<QuoteConfirmed>(Assert.IsType<ActiveRfq>(active.Lifecycle).QuoteState);
        Assert.Equal(quote.QuoteId, active.CurrentQuoteId);
    }

    [Fact]
    public void Withdraw_rejects_presented_and_requests_withdrawn_when_active()
    {
        var (rfq, _) = ConfirmQuote();
        var withdrawn = QuoteTransitions.Withdraw(rfq, rfq.Version);
        Assert.Equal(QuoteRequestReason.Withdrawn, withdrawn.QuoteRequestReason);
        var presented = RfqLifecycleTransitions.Present(rfq, rfq.Version);
        Assert.Throws<DomainRuleViolationException>(
            () => QuoteTransitions.Withdraw(presented, presented.Version));
    }

    [Fact]
    public void Expire_returns_presented_or_active_to_expired_request()
    {
        var (rfq, quote) = ConfirmQuote();
        var presented = RfqLifecycleTransitions.Present(rfq, rfq.Version);
        var expired = QuoteTransitions.Expire(presented, quote.QuoteId, presented.Version);
        Assert.IsType<ActiveRfq>(expired.Lifecycle);
        Assert.Equal(QuoteRequestReason.Expired, expired.QuoteRequestReason);
    }

    [Fact]
    public void Cancel_and_reopen_reset_ownership()
    {
        var owned = RfqOwnershipTransitions.PickUp(Open(), Trader, Open().Version);
        var cancelled = RfqLifecycleTransitions.Cancel(owned, owned.Version);
        Assert.Null(cancelled.Ownership);
        var reopened = RfqLifecycleTransitions.Reopen(cancelled, cancelled.Version);
        Assert.IsType<Unowned>(reopened.Ownership);
        Assert.Equal(QuoteRequestReason.Reopened, reopened.QuoteRequestReason);
    }

    [Fact]
    public void Release_changes_owned_to_unowned_and_preserves_assigned_trader()
    {
        var original = Open();
        var picked = RfqOwnershipTransitions.PickUp(original, Trader, original.Version);
        Assert.IsType<Unowned>(original.Ownership);
        Assert.IsType<Owned>(picked.Ownership);
        var released = RfqOwnershipTransitions.Release(picked, picked.Version);
        Assert.IsType<Unowned>(released.Ownership);
        Assert.Equal(Trader, released.AssignedTraderId);
    }

    [Fact]
    public void Release_rejects_an_unowned_rfq()
    {
        var rfq = Open();
        Assert.Throws<DomainRuleViolationException>(
            () => RfqOwnershipTransitions.Release(rfq, rfq.Version));
    }

    [Fact]
    public void Take_over_changes_the_target_trader_and_keeps_owned_state()
    {
        var currentOwner = UserId.Create("current");
        var target = UserId.Create("target");
        var open = Open();
        var assigned = RfqOwnershipTransitions.Assign(open, currentOwner, open.Version);
        var owned = RfqOwnershipTransitions.PickUp(assigned, currentOwner, assigned.Version);
        var takenOver = RfqOwnershipTransitions.TakeOver(owned, target, owned.Version);
        Assert.Equal(target, takenOver.AssignedTraderId);
        Assert.IsType<Owned>(takenOver.Ownership);
    }

    [Fact]
    public void Take_over_requires_a_target_trader()
    {
        var open = Open();
        var owned = RfqOwnershipTransitions.PickUp(open, Trader, open.Version);
        Assert.Throws<DomainValidationException>(
            () => RfqOwnershipTransitions.TakeOver(owned, null!, owned.Version));
    }

    [Fact]
    public void Assign_changes_the_target_trader_and_keeps_unowned_state()
    {
        var target = UserId.Create("target");
        var open = Open();
        var assigned = RfqOwnershipTransitions.Assign(open, target, open.Version);
        Assert.Equal(target, assigned.AssignedTraderId);
        Assert.IsType<Unowned>(assigned.Ownership);
    }

    [Fact]
    public void Quote_confirm_returns_consistent_case_and_snapshot()
    {
        var (rfq, quote) = ConfirmQuote();
        Assert.Equal(quote.QuoteId, rfq.CurrentQuoteId);
        Assert.Equal(quote.RevisionId, rfq.CurrentRevision.RevisionId);
        Assert.Equal(QuoteStatus.Quoted, rfq.QuoteStatus);
    }

    [Fact]
    public void Quote_confirm_rejects_working_quote_for_another_revision()
    {
        var rfq = Open();
        var other = WorkingQuote.Restore(
            RevisionId.New(), WorkingQuoteMode.Calculated, Payload(), null,
            new StateVersion(1), Now, Trader, Now, Trader);
        Assert.Throws<DomainRuleViolationException>(() => QuoteTransitions.Confirm(
            rfq, other, QuoteId.New(),
            new QuoteConfirmation(Trader, Now, new QuoteExpiry.None())));
    }

    [Fact]
    public void Working_quote_transitions_return_new_values_and_clear_manual_on_switch()
    {
        var rfq = Open();
        var initial = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        var calculated = WorkingQuoteTransitions.ApplyCalculated(
            initial, Payload(), initial.Version, Trader, Now);
        var manual = WorkingQuoteTransitions.SwitchMode(
            calculated, WorkingQuoteMode.Manual, calculated.Version, Trader, Now);
        Assert.Null(manual.Manual!.Price);
        var edited = WorkingQuoteTransitions.UpdateManual(
            manual, 99m, 1.2m, manual.Version, Trader, Now);
        Assert.Equal(99m, edited.Manual!.Price);
        Assert.Null(initial.Calculated);
    }

    [Theory]
    [InlineData(QuoteRequestReason.Revised)]
    [InlineData(QuoteRequestReason.Reopened)]
    [InlineData(QuoteRequestReason.Expired)]
    [InlineData(QuoteRequestReason.Withdrawn)]
    public void Initial_working_quote_rejects_non_initial_quote_requests(
        QuoteRequestReason reason)
    {
        var rfq = Requested(reason);
        Assert.Throws<DomainRuleViolationException>(
            () => WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now));
    }

    [Fact]
    public void Initial_working_quote_rejects_confirmed_and_presented_quotes()
    {
        var (confirmed, _) = ConfirmQuote();
        Assert.Throws<DomainRuleViolationException>(
            () => WorkingQuoteFactory.CreateInitialFor(confirmed, Trader, Now));

        var presented = RfqLifecycleTransitions.Present(confirmed, confirmed.Version);
        Assert.Throws<DomainRuleViolationException>(
            () => WorkingQuoteFactory.CreateInitialFor(presented, Trader, Now));
    }

    [Fact]
    public void Initial_working_quote_rejects_non_open_lifecycle_states()
    {
        var draft = Draft();
        var open = Open();
        var cancelled = RfqLifecycleTransitions.Cancel(open, open.Version);
        var (quoted, _) = ConfirmQuote();
        var closed = RfqLifecycleTransitions.Close(
            quoted, RfqStatus.Hit, quoted.Version).Rfq;

        foreach (var rfq in new[] { draft, cancelled, closed })
        {
            Assert.Throws<DomainRuleViolationException>(
                () => WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now));
        }
    }

    [Fact]
    public void Amendment_confirm_supersedes_and_requests_revised()
    {
        var rfq = Open();
        var save = AmendmentTransitions.SaveDraft(
            rfq, RevisionId.New(), new RevisionTerms(2_000_000, Today.AddDays(3),
                Today.AddDays(2), "changed"), Sales, Now, rfq.Version);
        var confirm = AmendmentTransitions.Confirm(
            save.Rfq, Today, Sales, Now, save.Rfq.Version, save.DraftRevision.Version);
        Assert.Equal(RevisionStatus.Superseded, confirm.SupersededRevision.Status);
        Assert.Equal(RevisionStatus.Confirmed, confirm.ConfirmedRevision.Status);
        Assert.Equal(confirm.ConfirmedRevision.RevisionId, confirm.Rfq.CurrentRevision.RevisionId);
        Assert.Equal(QuoteRequestReason.Revised, confirm.Rfq.QuoteRequestReason);
        Assert.Null(confirm.Rfq.CurrentQuoteId);
    }

    [Fact]
    public void Amendment_discard_returns_discarded_revision()
    {
        var rfq = Open();
        var save = AmendmentTransitions.SaveDraft(
            rfq, RevisionId.New(), rfq.CurrentRevision.Terms, Sales, Now, rfq.Version);
        var discarded = AmendmentTransitions.Discard(
            save.Rfq, save.Rfq.Version, save.DraftRevision.Version);
        Assert.Equal(RevisionStatus.Discarded, discarded.DiscardedRevision.Status);
        Assert.Null(discarded.Rfq.PendingDraftRevision);
    }

    [Fact]
    public void Close_discards_pending_amendment_and_requires_quote()
    {
        var open = Open();
        Assert.Throws<DomainRuleViolationException>(
            () => RfqLifecycleTransitions.Close(open, RfqStatus.Hit, open.Version));
        var (quoted, _) = ConfirmQuote();
        var save = AmendmentTransitions.SaveDraft(
            quoted, RevisionId.New(), quoted.CurrentRevision.Terms,
            Sales, Now, quoted.Version);
        var closed = RfqLifecycleTransitions.Close(
            save.Rfq, RfqStatus.Hit, save.Rfq.Version);
        Assert.Equal(RevisionStatus.Discarded, closed.DiscardedRevision!.Status);
        Assert.Equal(RfqStatus.Hit, closed.Rfq.Status);
    }

    [Fact]
    public void Case_memo_transitions_are_immutable_and_versioned()
    {
        var memo = CaseMemo.Create(new CaseId(1));
        var updated = CaseMemoTransitions.UpdateSales(memo, " note ", memo.Version);
        Assert.Equal("", memo.SalesMemo);
        Assert.Equal("note", updated.SalesMemo);
        Assert.Equal(2, updated.Version.Value);
    }

    private static RfqCase Open()
    {
        var draft = Draft();
        return RfqLifecycleTransitions.ConfirmInitial(
            draft, draft.CurrentRevision.Terms, Trader, Today, Sales, Now,
            draft.CurrentRevision.Version);
    }

    private static RfqCase Draft() =>
        RfqCase.CreateDraft(
            new CaseId(1), RevisionId.New(), ClientId.Create("c"), SecurityId.Create("s"),
            CategoryId.Create("cat"), Trader,
            new RevisionTerms(1_000_000, Today.AddDays(2), Today.AddDays(2), ""),
            Sales, Now);

    private static RfqCase Requested(QuoteRequestReason reason)
    {
        if (reason == QuoteRequestReason.Reopened)
        {
            var open = Open();
            var cancelled = RfqLifecycleTransitions.Cancel(open, open.Version);
            return RfqLifecycleTransitions.Reopen(cancelled, cancelled.Version);
        }

        if (reason == QuoteRequestReason.Revised)
        {
            var open = Open();
            var saved = AmendmentTransitions.SaveDraft(
                open, RevisionId.New(), open.CurrentRevision.Terms,
                Sales, Now, open.Version);
            return AmendmentTransitions.Confirm(
                saved.Rfq, Today, Sales, Now, saved.Rfq.Version,
                saved.DraftRevision.Version).Rfq;
        }

        var (confirmed, quote) = ConfirmQuote();
        return reason switch
        {
            QuoteRequestReason.Expired => QuoteTransitions.Expire(
                confirmed, quote.QuoteId, confirmed.Version),
            QuoteRequestReason.Withdrawn => QuoteTransitions.Withdraw(
                confirmed, confirmed.Version),
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }

    private static (RfqCase Rfq, ConfirmedQuote Quote) ConfirmQuote()
    {
        var rfq = Open();
        var working = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        working = WorkingQuoteTransitions.ApplyCalculated(
            working, Payload(), working.Version, Trader, Now);
        var result = QuoteTransitions.Confirm(
            rfq, working, QuoteId.New(),
            new QuoteConfirmation(Trader, Now, new QuoteExpiry.After(TimeSpan.FromMinutes(5))));
        return (result.Rfq, result.ConfirmedQuote);
    }

    private static CalculatedQuotePayload Payload() => new(
        CalculationDriver.Price, 100m, 100m, 1m, 1m, 0m, 1m, 1m, 10m, 10m);
}
