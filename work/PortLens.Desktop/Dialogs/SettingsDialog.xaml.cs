using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using PortLens.Models;
using PortLens.Desktop.Properties;
using PortLens.Desktop.Services;
using PortLens.Desktop.Settings;
using PortLens.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace PortLens.Desktop.Dialogs;

public partial class SettingsDialog : System.Windows.Controls.UserControl
{
    private static readonly Dictionary<string, BitmapImage> BadgeImageCache = new();

    private readonly List<int> _excludedPorts;
    private readonly List<FrameworkRuleEditor> _frameworkRuleEditors = [];
    private readonly UpdateCheckService _updateCheckService;
    private FrameworkRuleEditor? _selectedFrameworkRuleEditor;
    private string _latestVersion = "";
    private UpdateInfo? _updateInfo;

    internal event Action<UpdateInfo>? OnUpdateRequested;

    internal SettingsDialog(SettingsDialogState state, UpdateCheckService updateCheckService)
    {
        InitializeComponent();
        _updateCheckService = updateCheckService;
        _excludedPorts = state.ExcludedPorts.Order().ToList();

        ShowSystemPortsToggle.IsChecked = state.ShowSystemPorts;
        RememberPlacementToggle.IsChecked = state.RememberWindowPlacement;
        CloseToTrayToggle.IsChecked = state.CloseToTray;
        GroupByProjectToggle.IsChecked = state.GroupByProject;
        ShowAppMetricsToggle.IsChecked = state.ShowAppMetrics;
        SelectRefreshInterval(state.RefreshIntervalSeconds);
        SelectTheme(state.Theme);
        SelectLanguage(state.Language);
        BuildFontCombo(ChineseFontCombo, state.ChineseFontFamily);
        BuildFontCombo(EnglishFontCombo, state.EnglishFontFamily);
        BuildFrameworkRules(state.FrameworkRules, state.EnabledFrameworks);
        RenderBlacklist();
        UpdateBlacklistTabTitle();
        InitializeAboutTab(state.Version, state.LatestVersion, state.UpdateInfo);
    }

    private void SettingsDialog_Loaded(object sender, RoutedEventArgs e)
    {
        SelectTab(0);
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string tag } && int.TryParse(tag, out var index))
        {
            SelectTab(index);
        }
    }

    private void SelectTab(int index)
    {
        GeneralTabContent.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        RulesTabContent.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        BlacklistTabContent.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        AboutTabContent.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

        UpdateTabButtonState(TabGeneralButton, index == 0);
        UpdateTabButtonState(TabRulesButton, index == 1);
        UpdateTabButtonState(TabBlacklistButton, index == 2);
        UpdateTabButtonState(TabAboutButton, index == 3);
    }

    private void UpdateTabButtonState(WpfButton button, bool isSelected)
    {
        button.BorderThickness = new Thickness(0, 0, 0, isSelected ? 2 : 0);
        button.Foreground = isSelected
            ? TryFindResource("PortLensBrandBrush") as System.Windows.Media.Brush
            : TryFindResource("PortLensSubtitleBrush") as System.Windows.Media.Brush;
    }

    private void SelectTheme(PortLens.Desktop.Settings.Theme theme)
    {
        foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == theme.ToString())
            {
                ThemeCombo.SelectedItem = item;
                return;
            }
        }

        ThemeCombo.SelectedIndex = 0;
    }

    private void SelectLanguage(string language)
    {
        foreach (var item in LanguageCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == language)
            {
                LanguageCombo.SelectedItem = item;
                return;
            }
        }

        LanguageCombo.SelectedIndex = 0;
    }

    private void SelectRefreshInterval(int seconds)
    {
        foreach (var item in RefreshIntervalCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == seconds.ToString())
            {
                RefreshIntervalCombo.SelectedItem = item;
                return;
            }
        }

        RefreshIntervalCombo.SelectedIndex = 1;
    }

    private sealed record FontOption(string Name, string DisplayName, System.Windows.Media.FontFamily FontFamily);

    private void BuildFontCombo(WpfComboBox comboBox, string selectedFont)
    {
        var options = new List<FontOption>
        {
            new("", Properties.Resources.GetString("FontSystemDefault"), System.Windows.SystemFonts.MessageFontFamily)
        };

        foreach (var fontName in FontService.GetInstalledFontFamilies())
        {
            options.Add(new FontOption(fontName, fontName, new System.Windows.Media.FontFamily(fontName)));
        }

        comboBox.ItemsSource = options;
        comboBox.DisplayMemberPath = "DisplayName";
        comboBox.SelectedValuePath = "Name";
        comboBox.SelectedValue = selectedFont;
    }

    private string GetSelectedFont(WpfComboBox comboBox)
    {
        return comboBox.SelectedValue?.ToString() ?? "";
    }

    private void BuildFrameworkRules(IReadOnlyList<FrameworkRule> rules, IReadOnlySet<string> enabledFrameworks)
    {
        FrameworkRulesList.Children.Clear();
        FrameworkRuleDetailsHost.Content = null;
        _frameworkRuleEditors.Clear();
        _selectedFrameworkRuleEditor = null;

        var sourceRules = rules.Count > 0 ? rules : FrameworkRules.CloneDefaults();
        foreach (var rule in sourceRules)
        {
            AddFrameworkRuleEditor(rule, enabledFrameworks.Contains(rule.Name));
        }

        RenderFrameworkRuleList();
        SelectFrameworkRule(_frameworkRuleEditors.FirstOrDefault());
    }

    private void AddFrameworkRuleEditor(FrameworkRule rule, bool isEnabled)
    {
        var details = new StackPanel();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var enabled = new WpfCheckBox
        {
            IsChecked = isEnabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        header.Children.Add(enabled);

        var name = new WpfTextBox
        {
            Text = rule.Name,
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedTextBox"),
            MinWidth = 160,
            Margin = new Thickness(0, 0, 10, 0)
        };
        HintAssist.SetHint(name, "Framework name");
        Grid.SetColumn(name, 1);
        header.Children.Add(name);

        var remove = new WpfButton
        {
            Content = Properties.Resources.GetString("ButtonRemove"),
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
            MinWidth = 76,
            Height = 32,
            Padding = new Thickness(8, 0, 8, 0)
        };
        Grid.SetColumn(remove, 2);
        header.Children.Add(remove);
        details.Children.Add(header);

        var processKeywords = CreateRuleTextBox("Process keywords", ToCsv(rule.ProcessNameKeywords));
        var commandKeywords = CreateRuleTextBox("Command keywords", ToCsv(rule.CommandLineKeywords));
        var pathKeywords = CreateRuleTextBox("Path keywords", ToCsv(rule.PathKeywords));
        var defaultPorts = CreateRuleTextBox("Default ports", string.Join(", ", rule.DefaultPorts));

        details.Children.Add(processKeywords);
        details.Children.Add(commandKeywords);
        details.Children.Add(pathKeywords);
        details.Children.Add(defaultPorts);

        var navName = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = TryFindResource("PortLensSectionTitleBrush") as System.Windows.Media.Brush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var navSummary = new TextBlock
        {
            FontSize = 11,
            Foreground = TryFindResource("PortLensTextBrush") as System.Windows.Media.Brush,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var navContent = new Grid();
        navContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        navContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var navStatus = new Border
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 5, 9, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = TryFindResource("PortLensBrandBrush") as System.Windows.Media.Brush
        };
        navContent.Children.Add(navStatus);

        var navText = new StackPanel();
        navText.Children.Add(navName);
        navText.Children.Add(navSummary);
        Grid.SetColumn(navText, 1);
        navContent.Children.Add(navText);

        var navButton = new WpfButton
        {
            Content = navContent,
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignFlatButton"),
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8, 8, 8),
            Margin = new Thickness(0, 0, 0, 4),
            BorderThickness = new Thickness(0)
        };

        var editor = new FrameworkRuleEditor(
            navButton,
            navStatus,
            navName,
            navSummary,
            details,
            enabled,
            name,
            processKeywords,
            commandKeywords,
            pathKeywords,
            defaultPorts);
        _frameworkRuleEditors.Add(editor);

        navButton.Click += (_, _) => SelectFrameworkRule(editor);
        remove.Click += (_, _) =>
        {
            var index = _frameworkRuleEditors.IndexOf(editor);
            _frameworkRuleEditors.Remove(editor);
            if (ReferenceEquals(_selectedFrameworkRuleEditor, editor))
            {
                SelectFrameworkRule(_frameworkRuleEditors.ElementAtOrDefault(Math.Min(index, _frameworkRuleEditors.Count - 1)));
            }

            RenderFrameworkRuleList();
        };
        enabled.Checked += (_, _) => UpdateFrameworkRuleNav(editor);
        enabled.Unchecked += (_, _) => UpdateFrameworkRuleNav(editor);
        name.TextChanged += (_, _) => UpdateFrameworkRuleNav(editor);
        processKeywords.TextChanged += (_, _) => UpdateFrameworkRuleNav(editor);
        commandKeywords.TextChanged += (_, _) => UpdateFrameworkRuleNav(editor);
        pathKeywords.TextChanged += (_, _) => UpdateFrameworkRuleNav(editor);
        defaultPorts.TextChanged += (_, _) => UpdateFrameworkRuleNav(editor);
        UpdateFrameworkRuleNav(editor);
    }

    private WpfTextBox CreateRuleTextBox(string hint, string text)
    {
        var textBox = new WpfTextBox
        {
            Text = text,
            Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedTextBox"),
            Margin = new Thickness(28, 0, 0, 8)
        };
        HintAssist.SetHint(textBox, hint);
        return textBox;
    }

    private void FrameworkRuleSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RenderFrameworkRuleList();
    }

    private void RenderFrameworkRuleList()
    {
        FrameworkRulesList.Children.Clear();
        var query = FrameworkRuleSearchBox.Text.Trim();
        foreach (var editor in _frameworkRuleEditors.Where(editor => MatchesFrameworkRuleSearch(editor, query)))
        {
            FrameworkRulesList.Children.Add(editor.NavButton);
        }
    }

    private static bool MatchesFrameworkRuleSearch(FrameworkRuleEditor editor, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || editor.Name.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
            || editor.ProcessKeywords.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
            || editor.CommandKeywords.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
            || editor.PathKeywords.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
            || editor.DefaultPorts.Text.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectFrameworkRule(FrameworkRuleEditor? editor)
    {
        _selectedFrameworkRuleEditor = editor;
        FrameworkRuleDetailsHost.Content = editor is null
            ? CreateNoFrameworkRuleSelectedView()
            : editor.DetailsPanel;
        foreach (var item in _frameworkRuleEditors)
        {
            item.NavButton.BorderThickness = ReferenceEquals(item, editor) ? new Thickness(2, 0, 0, 0) : new Thickness(0);
            item.NavButton.BorderBrush = ReferenceEquals(item, editor)
                ? TryFindResource("PortLensBrandBrush") as System.Windows.Media.Brush
                : null;
        }
    }

    private TextBlock CreateNoFrameworkRuleSelectedView()
    {
        return new TextBlock
        {
            Text = "Select a rule to edit, or add a new rule.",
            Foreground = TryFindResource("PortLensTextBrush") as System.Windows.Media.Brush,
            FontSize = 13,
            Margin = new Thickness(0, 18, 0, 0)
        };
    }

    private void UpdateFrameworkRuleNav(FrameworkRuleEditor editor)
    {
        var name = editor.Name.Text.Trim();
        editor.NavName.Text = string.IsNullOrWhiteSpace(name) ? "Unnamed rule" : name;
        editor.NavSummary.Text = BuildFrameworkRuleSummary(editor);
        editor.NavStatus.Opacity = editor.Enabled.IsChecked == true ? 1 : 0.25;

        if (!string.IsNullOrWhiteSpace(FrameworkRuleSearchBox.Text))
        {
            RenderFrameworkRuleList();
        }
    }

    private static string BuildFrameworkRuleSummary(FrameworkRuleEditor editor)
    {
        var processCount = ParseCsv(editor.ProcessKeywords.Text).Count();
        var commandCount = ParseCsv(editor.CommandKeywords.Text).Count();
        var pathCount = ParseCsv(editor.PathKeywords.Text).Count();
        var portCount = ParsePorts(editor.DefaultPorts.Text).Count();
        var parts = new List<string>();
        if (processCount > 0) parts.Add($"{processCount} process");
        if (commandCount > 0) parts.Add($"{commandCount} command");
        if (pathCount > 0) parts.Add($"{pathCount} path");
        if (portCount > 0) parts.Add($"{portCount} ports");
        return parts.Count > 0 ? string.Join(" · ", parts) : "No match keywords";
    }

    private static string ToCsv(IEnumerable<string> values)
        => string.Join(", ", values);

    private void AddFrameworkRuleButton_Click(object sender, RoutedEventArgs e)
    {
        AddFrameworkRuleEditor(new FrameworkRule { Name = "Custom" }, true);
        RenderFrameworkRuleList();
        SelectFrameworkRule(_frameworkRuleEditors.LastOrDefault());
    }

    private void ResetFrameworkRulesButton_Click(object sender, RoutedEventArgs e)
    {
        BuildFrameworkRules(FrameworkRules.CloneDefaults(), new HashSet<string>(FrameworkRules.DefaultNames(), StringComparer.OrdinalIgnoreCase));
    }

    private (IReadOnlyList<FrameworkRule> Rules, IReadOnlyList<string> EnabledFrameworks) CaptureFrameworkRules()
    {
        var rules = new List<FrameworkRule>();
        var enabled = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var editor in _frameworkRuleEditors)
        {
            var name = editor.Name.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
            {
                continue;
            }

            rules.Add(new FrameworkRule
            {
                Name = name,
                ProcessNameKeywords = ParseCsv(editor.ProcessKeywords.Text).ToList(),
                CommandLineKeywords = ParseCsv(editor.CommandKeywords.Text).ToList(),
                PathKeywords = ParseCsv(editor.PathKeywords.Text).ToList(),
                DefaultPorts = ParsePorts(editor.DefaultPorts.Text).ToList()
            });

            if (editor.Enabled.IsChecked == true)
            {
                enabled.Add(name);
            }
        }

        if (rules.Count == 0)
        {
            rules = FrameworkRules.CloneDefaults().ToList();
            enabled = FrameworkRules.DefaultNames().ToList();
        }

        if (enabled.Count == 0)
        {
            enabled = rules.Select(rule => rule.Name).ToList();
        }

        return (rules, enabled);
    }

    private static IEnumerable<string> ParseCsv(string text)
        => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<int> ParsePorts(string text)
        => ParseCsv(text)
            .Select(value => int.TryParse(value, out var port) ? port : 0)
            .Where(port => port is > 0 and <= 65535)
            .Distinct()
            .Order();

    private void RenderBlacklist()
    {
        BlacklistList.Children.Clear();
        if (_excludedPorts.Count == 0)
        {
            BlacklistList.Children.Add(new TextBlock
            {
                Text = Properties.Resources.GetString("BlacklistEmpty"),
                Foreground = TryFindResource("PortLensTextBrush") as System.Windows.Media.Brush ?? new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
                FontSize = 12
            });
            return;
        }

        foreach (var port in _excludedPorts.ToArray())
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 6)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = $":{port}",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                Foreground = TryFindResource("PortLensPortBrush") as System.Windows.Media.Brush ?? new SolidColorBrush(WpfColor.FromRgb(63, 123, 200)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var remove = new WpfButton
            {
                Content = Properties.Resources.GetString("ButtonRemove"),
                Style = (Style)System.Windows.Application.Current.FindResource("MaterialDesignOutlinedButton"),
                MinWidth = 76,
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0)
            };
            remove.Click += (_, _) =>
            {
                _excludedPorts.Remove(port);
                RenderBlacklist();
                UpdateBlacklistTabTitle();
            };
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            BlacklistList.Children.Add(row);
        }
    }

    private void UpdateBlacklistTabTitle()
    {
        TabBlacklistButton.Content = Properties.Resources.GetString("BlacklistTabFormat", _excludedPorts.Count);
    }

    private void InitializeAboutTab(string version, string latestVersion, UpdateInfo? updateInfo)
    {
        AboutVersionText.Text = $"v{version}";
        _latestVersion = latestVersion;
        _updateInfo = updateInfo;

        var encodedProject = Uri.EscapeDataString("corizhang/portlens");
        var encodedVersion = Uri.EscapeDataString(version);

        ProjectUrlBadge.Source = LoadCachedBadgeImage(
            $"https://img.shields.io/badge/GitHub-{encodedProject}-blue.png?logo=github");

        VersionBadge.Source = LoadCachedBadgeImage(
            $"https://img.shields.io/badge/{Properties.Resources.GetString("AboutVersion")}-{encodedVersion}-blue.png");

        LatestVersionBadge.Source = string.IsNullOrWhiteSpace(latestVersion)
            ? null
            : LoadCachedBadgeImage(
                $"https://img.shields.io/github/v/release/corizhang/portlens.png?label={Properties.Resources.GetString("AboutLatestVersionFormat").Replace("v{0}", "").Trim(':',' ')}&color=green");

        LicenseBadge.Source = LoadCachedBadgeImage("https://img.shields.io/github/license/corizhang/portlens.png");

        UpdateAboutUpdateStatus();
    }

    private static BitmapImage LoadCachedBadgeImage(string url)
    {
        if (BadgeImageCache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(url, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();

        BadgeImageCache[url] = image;
        return image;
    }

    private void UpdateAboutUpdateStatus()
    {
        if (_updateInfo is null)
        {
            AboutLatestVersionText.Text = string.Empty;
            AboutUpdateStatusText.Text = string.Empty;
            CheckUpdateButton.Content = Properties.Resources.GetString("AboutCheckUpdate");
            CheckUpdateButton.IsEnabled = true;
            return;
        }

        AboutLatestVersionText.Text = Properties.Resources.GetString("AboutLatestVersionFormat", _updateInfo.LatestVersion);

        if (_updateInfo.IsUpdateAvailable)
        {
            AboutUpdateStatusText.Text = Properties.Resources.GetString("UpdateAvailableFormat", _updateInfo.CurrentVersion, _updateInfo.LatestVersion);
            CheckUpdateButton.Content = Properties.Resources.GetString("AboutUpdateNow");
            CheckUpdateButton.IsEnabled = true;
        }
        else
        {
            AboutUpdateStatusText.Text = Properties.Resources.GetString("AboutNoUpdate");
            CheckUpdateButton.Content = Properties.Resources.GetString("AboutCheckUpdate");
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        AboutUpdateStatusText.Text = Properties.Resources.GetString("AboutChecking");

        try
        {
            _updateInfo = await _updateCheckService.CheckAsync();
            if (_updateInfo is null)
            {
                AboutUpdateStatusText.Text = Properties.Resources.GetString("AboutUpdateError");
                return;
            }

            UpdateAboutUpdateStatus();

            if (_updateInfo.IsUpdateAvailable)
            {
                OnUpdateRequested?.Invoke(_updateInfo);
            }
        }
        catch (Exception ex)
        {
            AboutUpdateStatusText.Text = Properties.Resources.GetString("AboutUpdateError");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void ProjectUrlHyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ProjectUrlBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/corizhang/portlens") { UseShellExecute = true });
        e.Handled = true;
    }

    private void VersionBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/corizhang/portlens/releases") { UseShellExecute = true });
        e.Handled = true;
    }

    private void LatestVersionBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/corizhang/portlens/releases/latest") { UseShellExecute = true });
        e.Handled = true;
    }

    private void LicenseBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/corizhang/portlens/blob/master/LICENSE") { UseShellExecute = true });
        e.Handled = true;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Close(new SettingsDialogResult { Reset = true });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedSeconds = RefreshIntervalCombo.SelectedItem is ComboBoxItem item &&
                              int.TryParse(item.Tag?.ToString(), out var seconds)
            ? seconds
            : 5;

        var selectedTheme = ThemeCombo.SelectedItem is ComboBoxItem themeItem &&
                            Enum.TryParse<PortLens.Desktop.Settings.Theme>(themeItem.Tag?.ToString(), out var theme)
            ? theme
            : PortLens.Desktop.Settings.Theme.Light;

        var selectedLanguage = LanguageCombo.SelectedItem is ComboBoxItem langItem
            ? langItem.Tag?.ToString() ?? "en-US"
            : "en-US";

        var frameworkRules = CaptureFrameworkRules();

        Close(new SettingsDialogResult
        {
            ShowSystemPorts = ShowSystemPortsToggle.IsChecked == true,
            RefreshIntervalSeconds = selectedSeconds,
            RememberWindowPlacement = RememberPlacementToggle.IsChecked == true,
            CloseToTray = CloseToTrayToggle.IsChecked == true,
            GroupByProject = GroupByProjectToggle.IsChecked == true,
            ShowAppMetrics = ShowAppMetricsToggle.IsChecked == true,
            Theme = selectedTheme,
            Language = selectedLanguage,
            ChineseFontFamily = GetSelectedFont(ChineseFontCombo),
            EnglishFontFamily = GetSelectedFont(EnglishFontCombo),
            EnabledFrameworks = frameworkRules.EnabledFrameworks,
            FrameworkRules = frameworkRules.Rules,
            ExcludedPorts = _excludedPorts.ToList()
        });
    }

    private void Close(SettingsDialogResult result)
    {
        DialogHost.CloseDialogCommand.Execute(result, this);
    }

    private sealed record FrameworkRuleEditor(
        WpfButton NavButton,
        Border NavStatus,
        TextBlock NavName,
        TextBlock NavSummary,
        StackPanel DetailsPanel,
        WpfCheckBox Enabled,
        WpfTextBox Name,
        WpfTextBox ProcessKeywords,
        WpfTextBox CommandKeywords,
        WpfTextBox PathKeywords,
        WpfTextBox DefaultPorts);
}
