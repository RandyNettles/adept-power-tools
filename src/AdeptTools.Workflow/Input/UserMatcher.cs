namespace AdeptTools.Workflow.Input;

public class UserMatcher
{
    private readonly List<AdeptUserEntry> _users;

    public UserMatcher(List<AdeptUserEntry> users) => _users = users;

    public UserMatchResult Match(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new UserMatchResult { InputValue = input, Confidence = MatchConfidence.None };

        var trimmed = input.Trim();

        // 1. Exact match on UserId (case-insensitive)
        var exactId = _users.FirstOrDefault(u =>
            string.Equals(u.UserId, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exactId is not null)
        {
            return new UserMatchResult
            {
                InputValue = trimmed,
                ResolvedUserId = exactId.UserId,
                MatchedDisplayName = exactId.DisplayName,
                Confidence = MatchConfidence.Exact
            };
        }

        // 2. Exact match on DisplayName (case-insensitive)
        var exactDisplay = _users.FirstOrDefault(u =>
            string.Equals(u.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exactDisplay is not null)
        {
            return new UserMatchResult
            {
                InputValue = trimmed,
                ResolvedUserId = exactDisplay.UserId,
                MatchedDisplayName = exactDisplay.DisplayName,
                Confidence = MatchConfidence.Strong
            };
        }

        // 3. Normalized name match — "Last, First" ↔ "First Last"
        var normalized = NormalizeName(trimmed);
        foreach (var user in _users)
        {
            var userNormalized = NormalizeName(user.DisplayName);
            if (string.Equals(normalized, userNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return new UserMatchResult
                {
                    InputValue = trimmed,
                    ResolvedUserId = user.UserId,
                    MatchedDisplayName = user.DisplayName,
                    Confidence = MatchConfidence.Strong
                };
            }
        }

        // 4. Partial match — last name + first initial
        var (inputFirst, inputLast) = SplitName(trimmed);
        if (!string.IsNullOrEmpty(inputLast))
        {
            var candidates = _users.Where(u =>
            {
                var (_, userLast) = SplitName(u.DisplayName);
                return string.Equals(userLast, inputLast, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (candidates.Count == 1)
            {
                return new UserMatchResult
                {
                    InputValue = trimmed,
                    ResolvedUserId = candidates[0].UserId,
                    MatchedDisplayName = candidates[0].DisplayName,
                    Confidence = MatchConfidence.Weak
                };
            }

            // Try first initial narrowing
            if (candidates.Count > 1 && !string.IsNullOrEmpty(inputFirst))
            {
                var initialMatch = candidates.FirstOrDefault(u =>
                {
                    var (userFirst, _) = SplitName(u.DisplayName);
                    return !string.IsNullOrEmpty(userFirst) &&
                           char.ToUpperInvariant(userFirst[0]) == char.ToUpperInvariant(inputFirst[0]);
                });

                if (initialMatch is not null)
                {
                    return new UserMatchResult
                    {
                        InputValue = trimmed,
                        ResolvedUserId = initialMatch.UserId,
                        MatchedDisplayName = initialMatch.DisplayName,
                        Confidence = MatchConfidence.Weak
                    };
                }
            }
        }

        // 5. Single-token match against last names (handles "Rameshbabu" style input)
        if (!trimmed.Contains(' ') && !trimmed.Contains(','))
        {
            var lastNameMatches = _users.Where(u =>
            {
                var (_, userLast) = SplitName(u.DisplayName);
                return string.Equals(userLast, trimmed, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (lastNameMatches.Count == 1)
            {
                return new UserMatchResult
                {
                    InputValue = trimmed,
                    ResolvedUserId = lastNameMatches[0].UserId,
                    MatchedDisplayName = lastNameMatches[0].DisplayName,
                    Confidence = MatchConfidence.Weak
                };
            }
        }

        // 6. No match
        return new UserMatchResult
        {
            InputValue = trimmed,
            Confidence = MatchConfidence.None
        };
    }

    /// <summary>
    /// Normalizes "Last, First" → "first last" (lowercase). 
    /// If no comma, returns lowercased input.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var parts = name.Split(',', 2);
        if (parts.Length == 2)
        {
            var first = parts[1].Trim();
            var last = parts[0].Trim();
            return $"{first} {last}".ToLowerInvariant();
        }

        return name.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Splits a name into (First, Last). Handles "Last, First" and "First Last" formats.
    /// </summary>
    private static (string First, string Last) SplitName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (string.Empty, string.Empty);

        // "Last, First" format
        var commaParts = name.Split(',', 2);
        if (commaParts.Length == 2)
        {
            return (commaParts[1].Trim(), commaParts[0].Trim());
        }

        // "First Last" format
        var spaceParts = name.Trim().Split(' ', 2);
        if (spaceParts.Length == 2)
        {
            return (spaceParts[0].Trim(), spaceParts[1].Trim());
        }

        // Single token — treat as last name
        return (string.Empty, name.Trim());
    }
}
