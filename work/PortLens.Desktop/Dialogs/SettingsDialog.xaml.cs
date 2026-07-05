using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using PortLens.Desktop.Properties;
using PortLens.Desktop.Services;
using PortLens.Desktop.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace PortLens.Desktop.Dialogs;

public partial class SettingsDialog : System.Windows.Controls.UserControl
{
    private readonly List<int> _excludedPorts;
    private readonly Dictionary<string, WpfCheckBox> _frameworkToggles = new(StringComparer.OrdinalIgnoreCase);
    private readonly UpdateCheckService _updateCheckService;
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
        BuildFrameworkRules(state.EnabledFrameworks);
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

    private void BuildFontCombo(WpfComboBox comboBox, string selectedFont)
    {
        comboBox.Items.Clear();
        comboBox.Items.Add(new ComboBoxItem
        {
            Content = Properties.Resources.GetString("FontSystemDefault"),
            Tag = ""
        });

        foreach (var fontName in FontService.GetInstalledFontFamilies())
        {
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = fontName,
                Tag = fontName,
                FontFamily = new System.Windows.Media.FontFamily(fontName)
            });
        }

        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == selectedFont)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private string GetSelectedFont(WpfComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? ""
            : "";
    }

    private void BuildFrameworkRules(IReadOnlySet<string> enabledFrameworks)
    {
        FrameworkRulesGrid.Children.Clear();
        _frameworkToggles.Clear();
        foreach (var framework in DesktopSettings.DefaultEnabledFrameworks)
        {
            var checkBox = new WpfCheckBox
            {
                Content = framework,
                IsChecked = enabledFrameworks.Contains(framework),
                Margin = new Thickness(0, 0, 10, 8)
            };
            _frameworkToggles[framework] = checkBox;
            FrameworkRulesGrid.Children.Add(checkBox);
        }
    }

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

        ProjectUrlBadge.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri($"https://img.shields.io/badge/GitHub-{encodedProject}-blue.png?logo=github", UriKind.Absolute));

        VersionBadge.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri($"https://img.shields.io/badge/{Properties.Resources.GetString("AboutVersion")}-{encodedVersion}-blue.png", UriKind.Absolute));

        LatestVersionBadge.Source = string.IsNullOrWhiteSpace(latestVersion)
            ? null
            : new System.Windows.Media.Imaging.BitmapImage(
                new Uri($"https://img.shields.io/github/v/release/corizhang/portlens.png?label={Properties.Resources.GetString("AboutLatestVersionFormat").Replace("v{0}", "").Trim(':',' ')}&color=green", UriKind.Absolute));

        LicenseBadge.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("https://img.shields.io/github/license/corizhang/portlens.png", UriKind.Absolute));

        UpdateAboutUpdateStatus();
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
            EnabledFrameworks = _frameworkToggles
                .Where(pair => pair.Value.IsChecked == true)
                .Select(pair => pair.Key)
                .ToList(),
            ExcludedPorts = _excludedPorts.ToList()
        });
    }

    private void Close(SettingsDialogResult result)
    {
        DialogHost.CloseDialogCommand.Execute(result, this);
    }
}
