namespace Siphon.Accounts.Tokens;

public sealed record CreateTokenRequest(string Name);

public sealed record TokenView(Guid Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt, bool Revoked);

public sealed record CreatedTokenView(Guid Id, string Name, string Prefix, string Secret);
