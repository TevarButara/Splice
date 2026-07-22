using Npgsql;

namespace Splice.Backend.Api;

public sealed record WalletView(long WarGemBalance, bool HasPendingRaid,
    RaidStakeTransactionView? PendingRaid, string LatestTransactionSummary);
public sealed record RaidStakeOfferView(string TargetName, string DifficultyBand, long EntryStake,
    long FullVictoryPayout, long OuterExtractionPayout, long InnerExtractionPayout, long CoreExtractionPayout);
public sealed record RaidStakeTransactionView(string RaidId, RaidStakeOfferView Offer, bool Settled,
    string Outcome, int BreachedRings, long Payout, long BalanceAfter, string StartedUtc,
    string SettledUtc, string SettlementNote);

public static class WalletFeature
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/wallet", async (HttpContext context, NpgsqlDataSource dataSource) =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
            try
            {
                var wallet = await LoadAsync(connection, null, RequestIdentityMiddleware.PlayerId(context),
                    context.RequestAborted);
                return Results.Ok(wallet);
            }
            catch (WalletNotFoundException)
            {
                return IdempotencyExecutor.ToResult(ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                    "WALLET_NOT_FOUND", "War Gem wallet was not found."));
            }
        });
    }

    public static async Task<WalletView> LoadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid playerId, CancellationToken cancellationToken)
    {
        long balance;
        Guid accountId;
        await using (var accountCommand = new NpgsqlCommand("""
            SELECT id, balance
              FROM splice.ledger_accounts
             WHERE owner_type = 'PLAYER' AND owner_id = @player AND currency_code = 'WAR_GEM'
            """, connection, transaction))
        {
            accountCommand.Parameters.AddWithValue("player", playerId);
            await using var reader = await accountCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new WalletNotFoundException();
            accountId = reader.GetGuid(0);
            balance = reader.GetInt64(1);
        }

        RaidStakeTransactionView? pending = null;
        await using (var pendingCommand = new NpgsqlCommand("""
            SELECT r.id, r.created_at, q.difficulty_band, q.attacker_stake,
                   q.full_victory_payout, q.outer_payout, q.inner_payout, q.core_payout,
                   defender.display_name
              FROM splice.raid_sessions r
              JOIN splice.raid_quotes q ON q.id = r.quote_id
              JOIN splice.players defender ON defender.id = r.defender_player_id
             WHERE r.attacker_player_id = @player
               AND r.state IN ('PREPARED', 'FUNDED', 'ACTIVE', 'SETTLING')
             ORDER BY r.created_at DESC LIMIT 1
            """, connection, transaction))
        {
            pendingCommand.Parameters.AddWithValue("player", playerId);
            await using var reader = await pendingCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var offer = new RaidStakeOfferView(reader.GetString(8), reader.GetString(2), reader.GetInt64(3),
                    reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7));
                pending = new RaidStakeTransactionView(reader.GetGuid(0).ToString("D"), offer, false,
                    "InProgress", 0, 0, balance, reader.GetFieldValue<DateTimeOffset>(1).ToString("O"),
                    string.Empty, string.Empty);
            }
        }

        var summary = "NO TRANSACTIONS";
        await using (var summaryCommand = new NpgsqlCommand("""
            SELECT t.transaction_type, p.amount, p.balance_after, t.id
              FROM splice.ledger_postings p
              JOIN splice.ledger_transactions t ON t.id = p.ledger_transaction_id
             WHERE p.ledger_account_id = @account
             ORDER BY p.id DESC LIMIT 1
            """, connection, transaction))
        {
            summaryCommand.Parameters.AddWithValue("account", accountId);
            await using var reader = await summaryCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                summary = $"{reader.GetString(0)} {reader.GetInt64(1):+0;-0;0} => {reader.GetInt64(2)} ({reader.GetGuid(3):D})";
        }

        return new WalletView(balance, pending is not null, pending, summary);
    }

    private sealed class WalletNotFoundException : Exception;
}
