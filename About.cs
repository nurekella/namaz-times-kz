using System.Diagnostics;
using System.Text.Json;

namespace NamazTimes;

/// "About": version, author, license, links, data sources and privacy in one card.
public class AboutForm : Form
{
    public static string RepoUrl => $"https://github.com/{Updates.Repo ?? "nurekella/namaz-times-kz"}";

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public AboutForm(Action openNews)
    {
        Theme.Apply(this, '\uE946');
        Text = L.T("About");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        int w = Theme.Dp(520);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.Dp(20)) };
        var icon = new PictureBox { Image = Theme.AppIcon(new Size(64, 64)).ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(Theme.Dp(56), Theme.Dp(56)), Margin = new Padding(0, 0, Theme.Dp(14), 0) };
        var name = Theme.Label("Namaz Times KZ", Theme.Text, 16f, FontStyle.Bold);
        name.Margin = new Padding(0, Theme.Dp(4), 0, 0);
        var version = Theme.Label($"{L.T("VersionWord")} {Updates.Current.ToString(3)}", Theme.Accent, 10f, FontStyle.Bold);
        version.Margin = Padding.Empty;
        var titles = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty };
        titles.Controls.AddRange([name, version]);
        var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, Theme.Dp(10)) };
        head.Controls.AddRange([icon, titles]);
        root.Controls.Add(head);
        root.Controls.Add(Para(L.T("AboutDesc"), Theme.Text, w));

        // label: value rows
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, Theme.Dp(8), 0, Theme.Dp(4)) };
        void Row(string key, Control value) { var l = Theme.Label(L.T(key), Theme.Muted, 9.5f); l.Anchor = AnchorStyles.Left | AnchorStyles.Top; grid.Controls.Add(l); grid.Controls.Add(value); }
        Row("AboutAuthor", Theme.Label("Nurbol Khamzauly", Theme.Text, 10f));
        Row("AboutLicense", Link("MIT", RepoUrl + "/blob/main/LICENSE"));
        Row("AboutSource", Link(RepoUrl.Replace("https://", ""), RepoUrl));
        Row("AboutTimes", Link(L.T("AboutTimesValue"), "https://muftyat.kz"));
        Row("Quran", Para(L.T("AboutQuranValue"), Theme.Text, w - Theme.Dp(150)));
        root.Controls.Add(grid);

        root.Controls.Add(Para(L.T("AboutPrivacy"), Theme.Muted, w));
        root.Controls.Add(Para(L.T("AboutNotOfficial"), NamesForm.Gold, w));

        var news = Theme.Button(L.T("News"), primary: true);
        news.Click += (_, _) => openNews();
        var report = Theme.Button(L.T("ReportProblem"));
        report.Click += (_, _) => OpenUrl(RepoUrl + "/issues");
        var github = Theme.Button("GitHub");
        github.Click += (_, _) => OpenUrl(RepoUrl);
        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, Theme.Dp(12), 0, 0) };
        buttons.Controls.AddRange([news, report, github]);
        root.Controls.Add(buttons);
        Controls.Add(root);
    }

    static Label Para(string text, Color color, int width) => new()
    {
        Text = text, AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = color, Font = Theme.UI(9.5f),
        Margin = new Padding(0, Theme.Dp(4), 0, Theme.Dp(6)), UseMnemonic = false,
    };

    static LinkLabel Link(string text, string url)
    {
        var l = new LinkLabel
        {
            Text = text, AutoSize = true, Font = Theme.UI(10f), LinkColor = Theme.Accent, ActiveLinkColor = Theme.Text,
            LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, Theme.Dp(7), 0, Theme.Dp(7)),
        };
        l.LinkClicked += (_, _) => OpenUrl(url);
        return l;
    }
}

/// "What's new": every version, newest first, from changelog.json (kk/ru/en).
public class NewsForm : Form
{
    public NewsForm()
    {
        Theme.Apply(this, '\uE70B');
        Text = L.T("News");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.Dp(640), Theme.Dp(700));
        Padding = new Padding(Theme.Dp(12));

        var body = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, Padding = new Padding(0, 0, Theme.Dp(8), Theme.Dp(12)) };
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
        scroll.Controls.Add(body);
        scroll.HandleCreated += (_, _) => Theme.DarkScrollbars(scroll);
        Controls.Add(scroll);

        var lang = L.Lang is "kk" or "en" ? L.Lang : "ru";
        var current = Updates.Current.ToString(3);
        int w = Theme.Dp(560);
        var log = JsonSerializer.Deserialize<List<JsonElement>>(typeof(NewsForm).Assembly.GetManifestResourceStream("changelog.json")!)!;
        body.SuspendLayout();
        foreach (var e in log)
        {
            var v = e.GetProperty("v").GetString()!;
            var date = DateTime.Parse(e.GetProperty("d").GetString()!).ToString("d MMMM yyyy", L.Culture);
            var card = new TableLayoutPanel
            {
                AutoSize = true, ColumnCount = 1, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, Theme.Dp(10)), MinimumSize = new Size(w + Theme.Dp(32), 0),
                Padding = new Padding(Theme.Dp(16), Theme.Dp(10), Theme.Dp(16), Theme.Dp(12)),
            };
            var title = Theme.Label($"v{v}   ·   {date}" + (v == current ? $"   ·   {L.T("CurrentVersion")}" : ""),
                v == current ? Theme.Accent : Theme.Text, 11f, FontStyle.Bold);
            title.Margin = new Padding(0, 0, 0, Theme.Dp(6));
            card.Controls.Add(title);
            foreach (var item in e.GetProperty(lang).EnumerateArray())
                card.Controls.Add(new Label
                {
                    Text = "•  " + item.GetString(), AutoSize = true, MaximumSize = new Size(w, 0), ForeColor = Theme.Text, Font = Theme.UI(10f),
                    Margin = new Padding(0, Theme.Dp(2), 0, Theme.Dp(2)), UseMnemonic = false,
                });
            body.Controls.Add(Theme.Round(card));
        }
        body.ResumeLayout();
    }
}
