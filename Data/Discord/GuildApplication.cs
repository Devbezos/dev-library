namespace DevClient.Data.Discord
{
    using global::Discord;
    public class GuildApplication
    {
        public int RowIndex { get; init; }  // 1-based sheet row number
        public bool IsPosted { get; init; }
        public DateTime Timestamp { get; init; }
        public string ContactInfoLabel { get; init; } = string.Empty;
        public string ContactInfo { get; init; } = string.Empty;
        public string ClassSpecLabel { get; init; } = string.Empty;
        public string ClassSpec { get; init; } = string.Empty;
        public string MulticlassingLabel { get; init; } = string.Empty;
        public string? Multiclassing { get; init; }
        public string CanMakeRaidTimesLabel { get; init; } = string.Empty;
        public string CanMakeRaidTimes { get; init; } = string.Empty;
        public string WarcraftLogsLabel { get; init; } = string.Empty;
        public string WarcraftLogs { get; init; } = string.Empty;
        public string? PrivateLogCredentials { get; init; }
        public string MythicExperienceLabel { get; init; } = string.Empty;
        public string MythicExperience { get; init; } = string.Empty;
        public string ReasonForLeavingLabel { get; init; } = string.Empty;
        public string ReasonForLeaving { get; init; } = string.Empty;
        public string WhyGuildLabel { get; init; } = string.Empty;
        public string WhyGuild { get; init; } = string.Empty;
        public string AnythingElseLabel { get; init; } = string.Empty;
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
            sb.AppendLine($"**{ContactInfoLabel}:** {ContactInfo}");
            sb.AppendLine($"**{ClassSpecLabel}:** {ClassSpec}");

            if (!string.IsNullOrWhiteSpace(Multiclassing))
                sb.AppendLine($"**{MulticlassingLabel}:** {Multiclassing}");

            sb.AppendLine($"**{CanMakeRaidTimesLabel}:** {CanMakeRaidTimes}");
            sb.AppendLine($"**{WarcraftLogsLabel}:** {WarcraftLogs}");
            sb.AppendLine($"**{MythicExperienceLabel}:**");
            sb.AppendLine(Blockquote(MythicExperience));
            sb.AppendLine($"**{ReasonForLeavingLabel}:**");
            sb.AppendLine(Blockquote(ReasonForLeaving));
            sb.AppendLine($"**{WhyGuildLabel}:**");
            sb.AppendLine(Blockquote(WhyGuild));

            if (!string.IsNullOrWhiteSpace(AnythingElse))
            {
                sb.AppendLine($"**{AnythingElseLabel}:**");
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

            AddField(builder, ContactInfoLabel, ContactInfo);
            AddField(builder, ClassSpecLabel, ClassSpec);

            if (!string.IsNullOrWhiteSpace(Multiclassing))
                AddField(builder, MulticlassingLabel, Multiclassing);

            AddField(builder, CanMakeRaidTimesLabel, CanMakeRaidTimes);
            AddField(builder, WarcraftLogsLabel, WarcraftLogs);
            AddField(builder, MythicExperienceLabel, MythicExperience);
            AddField(builder, ReasonForLeavingLabel, ReasonForLeaving);
            AddField(builder, WhyGuildLabel, WhyGuild);

            if (!string.IsNullOrWhiteSpace(AnythingElse))
                AddField(builder, AnythingElseLabel, AnythingElse);

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





