namespace dev_library.Data.Discord
{
    using global::Discord;
    public class GuildApplication
    {
        public int RowIndex { get; init; }  // 1-based sheet row number
        public bool IsPosted { get; init; }
        public DateTime Timestamp { get; init; }
        public string ContactInfo { get; init; }
        public string ClassSpec { get; init; }
        public string? Multiclassing { get; init; }
        public string CanMakeRaidTimes { get; init; }
        public string WarcraftLogs { get; init; }
        public string? PrivateLogCredentials { get; init; }
        public string MythicExperience { get; init; }
        public string ReasonForLeaving { get; init; }
        public string WhyReforged { get; init; }
        public string? AnythingElse { get; init; }

        public string ToDiscordMessage()
        {
            static string Blockquote(string text)
            {
                // Collapse consecutive blank lines to one to reduce Discord spacing
                var lines = text.Split('\n').Select(l => l.TrimEnd()).ToList();
                var result = new List<string>();
                foreach (var line in lines)
                {
                    var bqLine = line == string.Empty ? "> " : $"> {line}";
                    if (bqLine == "> " && result.Count > 0 && result[^1] == "> ") continue;
                    result.Add(bqLine);
                }
                // Trim trailing empty blockquote lines
                while (result.Count > 0 && result[^1] == "> ")
                    result.RemoveAt(result.Count - 1);
                return string.Join("\n", result);
            }

            var sb = new System.Text.StringBuilder();

            sb.AppendLine("## 📋 New Guild Application");
            sb.AppendLine($"**Contact / Character:** {ContactInfo}");
            sb.AppendLine($"**Class & Spec:** {ClassSpec}");

            if (!string.IsNullOrWhiteSpace(Multiclassing))
                sb.AppendLine($"**Multiclassing:** {Multiclassing}");

            sb.AppendLine($"**Raid Times (Mon–Thurs 8:45PM–12AM EST):** {CanMakeRaidTimes}");
            sb.AppendLine($"**Warcraft Logs:** {WarcraftLogs}");
            sb.AppendLine("**Mythic Raiding Experience:**");
            sb.AppendLine(Blockquote(MythicExperience));
            sb.AppendLine("**Reason for Leaving:**");
            sb.AppendLine(Blockquote(ReasonForLeaving));
            sb.AppendLine("**Why Reforged:**");
            sb.AppendLine(Blockquote(WhyReforged));

            if (!string.IsNullOrWhiteSpace(AnythingElse))
            {
                sb.AppendLine("**Anything Else:**");
                sb.AppendLine(Blockquote(AnythingElse));
            }

            sb.AppendLine();
            sb.Append($"-# Submitted {Timestamp:MMM d, yyyy 'at' h:mm tt} UTC");

            return sb.ToString();
        }

        public string? ToOfficerMessage()
        {
            if (string.IsNullOrWhiteSpace(PrivateLogCredentials)) return null;
            return $"**Private Log Credentials**:\n> {PrivateLogCredentials}";
        }

        public Embed ToEmbed()
        {
            var builder = new EmbedBuilder()
                .WithTitle("New Application")
                .WithColor(new Color(0x5865F2))
                .WithFooter($"Submitted {Timestamp:MMM d, yyyy 'at' h:mm tt} UTC");

            AddField(builder, "Contact information - Discord (case sensitive), Character name & server", ContactInfo);
            AddField(builder, "Main class & spec", ClassSpec);

            if (!string.IsNullOrWhiteSpace(Multiclassing))
                AddField(builder, "Are you comfortable multiclassing? (not required)", Multiclassing);

            AddField(builder, "Can u make 8:45PM-12:00AM EST Mon-Thurs?", CanMakeRaidTimes);
            AddField(builder, "Warcraft Logs - include a link for all characters played in recent tiers for progression", WarcraftLogs);
            AddField(builder, "What is your Mythic raiding experience? - name the guild you progged with & the circumstance for leaving that guild", MythicExperience);
            AddField(builder, "Reason for leaving current/previous guild?", ReasonForLeaving);
            AddField(builder, "Why do you want to join Reforged?", WhyReforged);

            if (!string.IsNullOrWhiteSpace(AnythingElse))
                AddField(builder, "Anything else you want us to know?", AnythingElse);

            return builder.Build();
        }

        private static void AddField(EmbedBuilder builder, string name, string value)
        {
            if (value.Length <= 1024)
            {
                builder.AddField(name, value);
                return;
            }
            for (int i = 0, part = 0; i < value.Length; i += 1024, part++)
                builder.AddField(part == 0 ? name : $"{name} (cont.)", value.Substring(i, Math.Min(1024, value.Length - i)));
        }
    }
}
