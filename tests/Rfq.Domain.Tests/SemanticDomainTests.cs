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
        Assert.Throws<DomainInvariantException>(() => new StateVersion(long.MaxValue).Next());
    }

    [Fact]
    public void Immutable_domain_values_keep_validation_and_record_semantics()
    {
        Assert.Throws<DomainValidationException>(() => new QuoteExpiry.After(TimeSpan.Zero));
        var terms = new RevisionTerms(1_000_000, Today, Today, "  note  ");
        Assert.Equal("note", terms.SalesAndTradingMessage);
        Assert.Equal(terms, new RevisionTerms(1_000_000, Today, Today, "note"));
        Assert.Equal(Payload(), Payload());
        Assert.Equal(new ManualQuotePayload(100m, 1m), new ManualQuotePayload(100m, 1m));
        var confirmation = new QuoteConfirmation(
            Trader,
            new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(9)),
            new QuoteExpiry.None());
        Assert.Equal(TimeSpan.Zero, confirmation.ConfirmedAt.Offset);
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
        (RfqCase rfq, ConfirmedQuote quote) = ConfirmQuote();
        RfqCase presented = RfqLifecycleTransitions.Present(rfq, rfq.Version);
        Assert.IsType<PresentedRfq>(presented.Lifecycle);
        Assert.Equal(quote.QuoteId, presented.CurrentQuoteId);

        RfqCase active = RfqLifecycleTransitions.Unpresent(presented, presented.Version);
        Assert.IsType<QuoteConfirmed>(Assert.IsType<ActiveRfq>(active.Lifecycle).QuoteState);
        Assert.Equal(quote.QuoteId, active.CurrentQuoteId);
    }

    [Fact]
    public void Withdraw_rejects_presented_and_requests_withdrawn_when_active()
    {
        (RfqCase rfq, ConfirmedQuote _) = ConfirmQuote();
        RfqCase withdrawn = QuoteTransitions.Withdraw(rfq, rfq.Version);
        Assert.Equal(QuoteRequestReason.Withdrawn, withdrawn.QuoteRequestReason);
        RfqCase presented = RfqLifecycleTransitions.Present(rfq, rfq.Version);
        Assert.Throws<DomainRuleViolationException>(
            () => QuoteTransitions.Withdraw(presented, presented.Version));
    }

    [Fact]
    public void Expire_returns_presented_or_active_to_expired_request()
    {
        (RfqCase rfq, ConfirmedQuote quote) = ConfirmQuote();
        RfqCase presented = RfqLifecycleTransitions.Present(rfq, rfq.Version);
        RfqCase expired = QuoteTransitions.Expire(presented, quote.QuoteId, presented.Version);
        Assert.IsType<ActiveRfq>(expired.Lifecycle);
        Assert.Equal(QuoteRequestReason.Expired, expired.QuoteRequestReason);
    }

    [Fact]
    public void Cancel_and_reopen_reset_ownership()
    {
        RfqCase owned = RfqOwnershipTransitions.PickUp(Open(), Trader, Open().Version);
        RfqCase cancelled = RfqLifecycleTransitions.Cancel(owned, owned.Version);
        Assert.Null(cancelled.Ownership);
        RfqCase reopened = RfqLifecycleTransitions.Reopen(cancelled, cancelled.Version);
        Assert.IsType<Unowned>(reopened.Ownership);
        Assert.Equal(QuoteRequestReason.Reopened, reopened.QuoteRequestReason);
    }

    [Fact]
    public void Close_and_outcome_correction_use_typed_lifecycle_states()
    {
        (RfqCase quoted, ConfirmedQuote quote) = ConfirmQuote();
        RfqCase hit = RfqLifecycleTransitions.CloseHit(
            quoted,
            Today,
            quoted.Version).Rfq;
        Assert.IsType<HitRfq>(hit.Lifecycle);
        Assert.Equal(quote.QuoteId, hit.ClosedQuoteId);
        Assert.Equal(Today, hit.ClosedBusinessDate);
        RfqCase away = RfqLifecycleTransitions.CorrectToAway(hit, hit.Version);
        Assert.IsType<AwayRfq>(away.Lifecycle);
        Assert.Equal(Today, away.ClosedBusinessDate);
        RfqCase corrected = RfqLifecycleTransitions.CorrectToHit(away, away.Version);
        Assert.IsType<HitRfq>(corrected.Lifecycle);
        Assert.Equal(Today, corrected.ClosedBusinessDate);
    }

    [Fact]
    public void Close_away_creates_an_away_state()
    {
        (RfqCase quoted, ConfirmedQuote quote) = ConfirmQuote();
        RfqCase away = RfqLifecycleTransitions.CloseAway(
            quoted,
            Today,
            quoted.Version).Rfq;
        Assert.IsType<AwayRfq>(away.Lifecycle);
        Assert.Equal(quote.QuoteId, away.ClosedQuoteId);
        Assert.Equal(Today, away.ClosedBusinessDate);
    }

    [Fact]
    public void Cancel_and_reopen_preserve_pending_amendment_without_reviving_quote()
    {
        (RfqCase quoted, ConfirmedQuote _) = ConfirmQuote();
        AmendmentSaveResult saved = AmendmentTransitions.SaveDraft(
            quoted,
            RevisionId.New(),
            quoted.CurrentRevision.Terms,
            Sales,
            Now,
            Today,
            quoted.Version);
        RfqCase cancelled = RfqLifecycleTransitions.Cancel(saved.Rfq, saved.Rfq.Version);
        Assert.IsType<CancelledRfq>(cancelled.Lifecycle);
        Assert.Equal(saved.DraftRevision.RevisionId, cancelled.PendingDraftRevision?.RevisionId);
        Assert.Null(cancelled.CurrentQuoteId);

        RfqCase reopened = RfqLifecycleTransitions.Reopen(cancelled, cancelled.Version);
        Assert.Equal(saved.DraftRevision.RevisionId, reopened.PendingDraftRevision?.RevisionId);
        Assert.Null(reopened.CurrentQuoteId);
        Assert.Equal(QuoteRequestReason.Reopened, reopened.QuoteRequestReason);
    }

    [Fact]
    public void Release_changes_owned_to_unowned_and_preserves_assigned_trader()
    {
        RfqCase original = Open();
        RfqCase picked = RfqOwnershipTransitions.PickUp(original, Trader, original.Version);
        Assert.IsType<Unowned>(original.Ownership);
        Assert.IsType<Owned>(picked.Ownership);
        RfqCase released = RfqOwnershipTransitions.Release(picked, picked.Version);
        Assert.IsType<Unowned>(released.Ownership);
        Assert.Equal(Trader, released.AssignedTraderId);
    }

    [Fact]
    public void Release_rejects_an_unowned_rfq()
    {
        RfqCase rfq = Open();
        Assert.Throws<DomainRuleViolationException>(
            () => RfqOwnershipTransitions.Release(rfq, rfq.Version));
    }

    [Fact]
    public void Take_over_changes_the_target_trader_and_keeps_owned_state()
    {
        var currentOwner = UserId.Create("current");
        var target = UserId.Create("target");
        RfqCase open = Open();
        RfqCase assigned = RfqOwnershipTransitions.Assign(open, currentOwner, open.Version);
        RfqCase owned = RfqOwnershipTransitions.PickUp(assigned, currentOwner, assigned.Version);
        RfqCase takenOver = RfqOwnershipTransitions.TakeOver(owned, target, owned.Version);
        Assert.Equal(target, takenOver.AssignedTraderId);
        Assert.IsType<Owned>(takenOver.Ownership);
    }

    [Fact]
    public void Take_over_requires_a_target_trader()
    {
        RfqCase open = Open();
        RfqCase owned = RfqOwnershipTransitions.PickUp(open, Trader, open.Version);
        Assert.Throws<DomainValidationException>(
            () => RfqOwnershipTransitions.TakeOver(owned, null!, owned.Version));
    }

    [Fact]
    public void Assign_changes_the_target_trader_and_keeps_unowned_state()
    {
        var target = UserId.Create("target");
        RfqCase open = Open();
        RfqCase assigned = RfqOwnershipTransitions.Assign(open, target, open.Version);
        Assert.Equal(target, assigned.AssignedTraderId);
        Assert.IsType<Unowned>(assigned.Ownership);
    }

    [Fact]
    public void Quote_confirm_returns_consistent_case_and_snapshot()
    {
        (RfqCase rfq, ConfirmedQuote quote) = ConfirmQuote();
        Assert.Equal(quote.QuoteId, rfq.CurrentQuoteId);
        Assert.Equal(quote.RevisionId, rfq.CurrentRevision.RevisionId);
        Assert.Equal(QuoteStatus.Quoted, rfq.QuoteStatus);
    }

    [Fact]
    public void Quote_confirm_rejects_working_quote_for_another_revision()
    {
        RfqCase rfq = Open();
        var other = WorkingQuote.Restore(
            RevisionId.New(),
            WorkingQuoteMode.Calculated,
            Payload(),
            null,
            new StateVersion(1),
            Now,
            Trader,
            Now,
            Trader);
        Assert.Throws<DomainRuleViolationException>(() => QuoteTransitions.Confirm(
            rfq,
            other,
            QuoteId.New(),
            new QuoteConfirmation(Trader, Now, new QuoteExpiry.None())));
    }

    [Fact]
    public void Working_quote_transitions_return_new_values_and_clear_manual_on_switch()
    {
        RfqCase rfq = Open();
        WorkingQuote initial = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        WorkingQuote calculated = WorkingQuoteTransitions.ApplyCalculated(
            initial, Payload(), initial.Version, Trader, Now);
        WorkingQuote manual = WorkingQuoteTransitions.SwitchMode(
            calculated, WorkingQuoteMode.Manual, calculated.Version, Trader, Now);
        Assert.Null(manual.Manual!.Price);
        WorkingQuote edited = WorkingQuoteTransitions.UpdateManual(
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
        RfqCase rfq = Requested(reason);
        Assert.Throws<DomainRuleViolationException>(
            () => WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now));
    }

    [Fact]
    public void Initial_working_quote_rejects_confirmed_and_presented_quotes()
    {
        (RfqCase confirmed, ConfirmedQuote _) = ConfirmQuote();
        Assert.Throws<DomainRuleViolationException>(
            () => WorkingQuoteFactory.CreateInitialFor(confirmed, Trader, Now));

        RfqCase presented = RfqLifecycleTransitions.Present(confirmed, confirmed.Version);
        Assert.Throws<DomainRuleViolationException>(
            () => WorkingQuoteFactory.CreateInitialFor(presented, Trader, Now));
    }

    [Fact]
    public void Initial_working_quote_rejects_non_open_lifecycle_states()
    {
        RfqCase draft = Draft();
        RfqCase open = Open();
        RfqCase cancelled = RfqLifecycleTransitions.Cancel(open, open.Version);
        (RfqCase quoted, ConfirmedQuote _) = ConfirmQuote();
        RfqCase closed = RfqLifecycleTransitions.CloseHit(
            quoted,
            Today,
            quoted.Version).Rfq;

        foreach (RfqCase? rfq in new[] { draft, cancelled, closed })
        {
            Assert.Throws<DomainRuleViolationException>(
                () => WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now));
        }
    }

    [Fact]
    public void Amendment_confirm_supersedes_and_requests_revised()
    {
        RfqCase rfq = Open();
        AmendmentSaveResult save = AmendmentTransitions.SaveDraft(
            rfq,
            RevisionId.New(),
            new RevisionTerms(
                2_000_000,
                Today.AddDays(3),
                Today.AddDays(2),
                "changed"),
            Sales,
            Now,
            Today,
            rfq.Version);
        AmendmentConfirmResult confirm = AmendmentTransitions.Confirm(
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
        RfqCase rfq = Open();
        AmendmentSaveResult save = AmendmentTransitions.SaveDraft(
            rfq, RevisionId.New(), rfq.CurrentRevision.Terms, Sales, Now, Today, rfq.Version);
        AmendmentDiscardResult discarded = AmendmentTransitions.Discard(
            save.Rfq, save.Rfq.Version, save.DraftRevision.Version);
        Assert.Equal(RevisionStatus.Discarded, discarded.DiscardedRevision.Status);
        Assert.Null(discarded.Rfq.PendingDraftRevision);
    }

    [Fact]
    public void Amendment_start_creates_zero_difference_draft_that_cannot_be_confirmed()
    {
        RfqCase rfq = Open();
        AmendmentSaveResult started = AmendmentTransitions.StartDraft(
            rfq, RevisionId.New(), Sales, Now, Today, rfq.Version);

        Assert.Equal(rfq.CurrentRevision.Terms, started.DraftRevision.Terms);
        Assert.NotNull(started.Rfq.PendingDraftRevision);
        Assert.Equal(Today, started.DraftRevision.DraftCreatedBusinessDate);
        Assert.Throws<DomainRuleViolationException>(() => AmendmentTransitions.Confirm(
            started.Rfq,
            Today,
            Sales,
            Now,
            started.Rfq.Version,
            started.DraftRevision.Version));
        Assert.NotNull(started.Rfq.PendingDraftRevision);
    }

    [Fact]
    public void Initial_draft_records_business_date_independently_from_timestamp()
    {
        var draft = RfqCase.CreateDraft(
            new CaseId(2),
            RevisionId.New(),
            ClientId.Create("c"),
            SecurityId.Create("s"),
            CategoryId.Create("cat"),
            Trader,
            new RevisionTerms(1_000_000, Today.AddDays(2), Today.AddDays(2), ""),
            Today,
            Sales,
            Now.AddDays(-1));

        Assert.Equal(Today, draft.CurrentRevision.DraftCreatedBusinessDate);
        Assert.NotEqual(
            DateOnly.FromDateTime(draft.CurrentRevision.CreatedAt.UtcDateTime),
            draft.CurrentRevision.DraftCreatedBusinessDate);
    }

    [Fact]
    public void Close_discards_pending_amendment_and_requires_quote()
    {
        RfqCase open = Open();
        Assert.Throws<DomainRuleViolationException>(
            () => RfqLifecycleTransitions.CloseHit(open, Today, open.Version));
        (RfqCase quoted, ConfirmedQuote _) = ConfirmQuote();
        AmendmentSaveResult save = AmendmentTransitions.SaveDraft(
            quoted,
            RevisionId.New(),
            quoted.CurrentRevision.Terms,
            Sales,
            Now,
            Today,
            quoted.Version);
        CloseTransitionResult closed = RfqLifecycleTransitions.CloseHit(
            save.Rfq,
            Today,
            save.Rfq.Version);
        Assert.Equal(RevisionStatus.Discarded, closed.DiscardedRevision!.Status);
        Assert.Equal(RfqStatus.Hit, closed.Rfq.Status);
    }

    [Fact]
    public void Sales_and_trader_memos_are_independently_versioned()
    {
        var sales = SalesMemo.Create(new CaseId(1));
        var trader = TraderMemo.Create(new CaseId(1));
        SalesMemo updatedSales = SalesMemoTransitions.Update(sales, " sales ", sales.Version);
        TraderMemo updatedTrader = TraderMemoTransitions.Update(trader, " trader ", trader.Version);
        Assert.Equal("", sales.Value);
        Assert.Equal("sales", updatedSales.Value);
        Assert.Equal("trader", updatedTrader.Value);
        Assert.Equal(2, updatedSales.Version.Value);
        Assert.Equal(2, updatedTrader.Version.Value);
    }

    private static RfqCase Open()
    {
        RfqCase draft = Draft();
        return RfqLifecycleTransitions.ConfirmInitial(
            draft,
            draft.CurrentRevision.Terms,
            Trader,
            Today,
            Sales,
            Now,
            draft.CurrentRevision.Version);
    }

    private static RfqCase Draft() =>
        RfqCase.CreateDraft(
            new CaseId(1),
            RevisionId.New(),
            ClientId.Create("c"),
            SecurityId.Create("s"),
            CategoryId.Create("cat"),
            Trader,
            new RevisionTerms(1_000_000, Today.AddDays(2), Today.AddDays(2), ""),
            Today,
            Sales,
            Now);

    private static RfqCase Requested(QuoteRequestReason reason)
    {
        if (reason == QuoteRequestReason.Reopened)
        {
            RfqCase open = Open();
            RfqCase cancelled = RfqLifecycleTransitions.Cancel(open, open.Version);
            return RfqLifecycleTransitions.Reopen(cancelled, cancelled.Version);
        }

        if (reason == QuoteRequestReason.Revised)
        {
            RfqCase open = Open();
            AmendmentSaveResult saved = AmendmentTransitions.SaveDraft(
                open,
                RevisionId.New(),
                new RevisionTerms(
                    open.CurrentRevision.Terms.Notional,
                    open.CurrentRevision.Terms.SettlementDate,
                    open.CurrentRevision.Terms.StandardSettlementDate,
                    "changed"),
                Sales,
                Now,
                Today,
                open.Version);
            return AmendmentTransitions.Confirm(
                saved.Rfq,
                Today,
                Sales,
                Now,
                saved.Rfq.Version,
                saved.DraftRevision.Version).Rfq;
        }

        (RfqCase confirmed, ConfirmedQuote quote) = ConfirmQuote();
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
        RfqCase rfq = Open();
        WorkingQuote working = WorkingQuoteFactory.CreateInitialFor(rfq, Trader, Now);
        working = WorkingQuoteTransitions.ApplyCalculated(
            working, Payload(), working.Version, Trader, Now);
        QuoteConfirmationResult result = QuoteTransitions.Confirm(
            rfq,
            working,
            QuoteId.New(),
            new QuoteConfirmation(Trader, Now, new QuoteExpiry.After(TimeSpan.FromMinutes(5))));
        return (result.Rfq, result.ConfirmedQuote);
    }

    private static CalculatedQuotePayload Payload() => new(
        CalculationDriver.Price, 100m, 100m, 1m, 1m, 0m, 1m, 1m, 10m, 10m);
}
