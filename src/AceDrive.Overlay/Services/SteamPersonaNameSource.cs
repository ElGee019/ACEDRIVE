using System.IO;
using System.Security;
using System.Text;
using AceDrive.Core;
using Microsoft.Win32;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Reads the Windows user's Steam display name (PersonaName) from
/// Steam/config/loginusers.vdf. AC EVO signs in with the Steam account and
/// exposes no readable name in its binary profile files or shared memory, so
/// this is the closest local source for "the name set in the game".
/// The opening screen keeps the field editable, so a missing Steam install
/// or a parse failure degrades to a blank name instead of blocking the flow.
/// </summary>
public sealed class SteamPersonaNameSource
{
    private const string SteamRegistryKey = @"HKEY_CURRENT_USER\Software\Valve\Steam";

    private readonly string? _loginUsersFile;
    private readonly string? _autoLoginUser;
    private readonly ILog _log;

    public SteamPersonaNameSource(ILog log)
        : this(ResolveLoginUsersFile(), ReadRegistry("AutoLoginUser"), log)
    { }

    /// <summary>Test seam: a loginusers.vdf path and auto-login account.</summary>
    public SteamPersonaNameSource(string? loginUsersFile, string? autoLoginUser, ILog log)
    {
        _loginUsersFile = loginUsersFile;
        _autoLoginUser = autoLoginUser;
        _log = log;
    }

    /// <summary>The Steam persona name, or null when it cannot be read. Never throws.</summary>
    public string? TryReadName()
    {
        if (string.IsNullOrWhiteSpace(_loginUsersFile))
        {
            _log.LogInfo("Steam loginusers.vdf not found; player name starts blank");
            return null;
        }

        try
        {
            if (!File.Exists(_loginUsersFile))
            {
                _log.LogWarning($"Steam loginusers.vdf missing at {_loginUsersFile}; player name starts blank");
                return null;
            }

            var users = ParseUsers(File.ReadAllText(_loginUsersFile));
            if (users.Count == 0)
            {
                _log.LogWarning($"Steam loginusers.vdf has no users at {_loginUsersFile}; player name starts blank");
                return null;
            }

            (string Account, string Persona, long Timestamp) user =
                FindAutoLogin(users) ?? MostRecent(users);
            if (string.IsNullOrWhiteSpace(user.Persona))
            {
                _log.LogWarning($"Steam user {user.Account} has no PersonaName; player name starts blank");
                return null;
            }

            string name = user.Persona.Trim();
            _log.LogInfo($"player name: {name} (Steam)");
            return name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Same external-boundary rule as every other reader: a missing,
            // locked or malformed Steam file leaves the field blank.
            _log.LogWarning($"Steam loginusers.vdf unreadable at {_loginUsersFile} ({ex.GetType().Name}); player name starts blank");
            return null;
        }
    }

    private (string Account, string Persona, long Timestamp)? FindAutoLogin(
        List<(string Account, string Persona, long Timestamp)> users)
    {
        if (string.IsNullOrWhiteSpace(_autoLoginUser))
        {
            return null;
        }

        foreach (var user in users)
        {
            if (string.Equals(user.Account, _autoLoginUser, StringComparison.OrdinalIgnoreCase))
            {
                return user;
            }
        }

        return null;
    }

    private static (string Account, string Persona, long Timestamp) MostRecent(
        List<(string Account, string Persona, long Timestamp)> users)
    {
        var newest = users[0];
        for (int i = 1; i < users.Count; i++)
        {
            if (users[i].Timestamp > newest.Timestamp)
            {
                newest = users[i];
            }
        }
        return newest;
    }

    private static string? ResolveLoginUsersFile()
    {
        string? root = ReadRegistry("SteamPath");
        if (string.IsNullOrWhiteSpace(root))
        {
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            root = Directory.Exists(fallback) ? fallback : null;
        }

        return root is null ? null : Path.Combine(root, "config", "loginusers.vdf");
    }

    private static string? ReadRegistry(string value)
    {
        try
        {
            return Registry.GetValue(SteamRegistryKey, value, null) as string;
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A locked or redirected registry is the same as "no Steam":
            // the opening screen accepts a typed name instead.
            return null;
        }
    }

    // Valve KeyValues tokenizer: quotes, braces and // comments are enough for
    // loginusers.vdf. Values keep escaped quotes; unquoted tokens are legal.
    private static List<(string Account, string Persona, long Timestamp)> ParseUsers(string text)
    {
        List<string> tokens = Tokenize(text);
        int index = 0;
        if (!TryTake(tokens, ref index, "users") || !TryTake(tokens, ref index, "{"))
        {
            throw new InvalidDataException("loginusers.vdf has no users block");
        }

        var users = new List<(string Account, string Persona, long Timestamp)>();
        while (index < tokens.Count && tokens[index] != "}")
        {
            index++; // Steam id
            if (!TryTake(tokens, ref index, "{"))
            {
                throw new InvalidDataException("loginusers.vdf user block is malformed");
            }

            string account = string.Empty;
            string persona = string.Empty;
            long timestamp = 0;
            while (index < tokens.Count && tokens[index] != "}")
            {
                string key = tokens[index++];
                if (index >= tokens.Count) break;
                if (tokens[index] == "{")
                {
                    SkipBlock(tokens, ref index);
                    continue;
                }

                string value = tokens[index++];
                switch (key)
                {
                    case "AccountName":
                        account = value;
                        break;
                    case "PersonaName":
                        persona = value;
                        break;
                    case "Timestamp":
                        if (!long.TryParse(value, out timestamp))
                        {
                            timestamp = 0;
                        }
                        break;
                }
            }

            if (!TryTake(tokens, ref index, "}"))
            {
                throw new InvalidDataException("loginusers.vdf user block is malformed");
            }
            users.Add((account, persona, timestamp));
        }

        return users;
    }

    private static void SkipBlock(List<string> tokens, ref int index)
    {
        int depth = 0;
        while (index < tokens.Count)
        {
            string token = tokens[index++];
            if (token == "{")
            {
                depth++;
            }
            else if (token == "}" && --depth == 0)
            {
                return;
            }
        }

        throw new InvalidDataException("loginusers.vdf block is not closed");
    }

    private static bool TryTake(List<string> tokens, ref int index, string expected)
    {
        if (index < tokens.Count && tokens[index] == expected)
        {
            index++;
            return true;
        }
        return false;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }

            if (c is '{' or '}')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            if (c == '"')
            {
                i++;
                var value = new StringBuilder();
                while (i < text.Length)
                {
                    if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        value.Append('"');
                        i += 2;
                        continue;
                    }
                    if (text[i] == '"')
                    {
                        i++;
                        break;
                    }
                    value.Append(text[i]);
                    i++;
                }
                tokens.Add(value.ToString());
                continue;
            }

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not '{' and not '}')
            {
                i++;
            }
            tokens.Add(text[start..i]);
        }

        return tokens;
    }
}
