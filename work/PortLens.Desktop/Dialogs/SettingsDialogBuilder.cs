using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using PortLens.Desktop.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace PortLens.Desktop.Dialogs;

internal sealed class SettingsDialogBuilder
{
    private readonly bool _showSystemPorts;
    private readonly int _refreshIntervalSeconds;
    private readonly bool _rememberWindowPlacement;
    private readonly bool _closeToTray;
    private readonly bool _groupByProject;
    private readonly IReadOnlySet<int> _excludedPorts;
    private readonly IReadOnlySet<string> _enabledFrameworks;

    public SettingsDialogBuilder(
        bool showSystemPorts,
        int refreshIntervalSeconds,
        bool rememberWindowPlacement,
        bool closeToTray,
        bool groupByProject,
        IReadOnlySet<int> excludedPorts,
        IReadOnlySet<string> enabledFrameworks)
    {
        _showSystemPorts = showSystemPorts;
        _refreshIntervalSeconds = refreshIntervalSeconds;
        _rememberWindowPlacement = rememberWindowPlacement;
        _closeToTray = closeToTray;
        _groupByProject = groupByProject;
        _excludedPorts = excludedPorts;
        _enabledFrameworks = enabledFrameworks;
    }

    public FrameworkElement Build()
    {
        var shell = new Grid
        {
            Width = 560,
            MaxHeight = 680,
            Margin = new Thickness(24)
        };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16)
        };
        header.Children.Add(new PackIcon
        {
            Kind = PackIconKind.CogOutline,
            Width = 28,
            Height = 28,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(103, 58, 183)),
            Margin = new Thickness(0, 0, 12, 0)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 27, 24)),
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(header, 0);
        shell.Children.Add(header);

        var general = BuildGeneralSection(
            out var showSystemPorts,
            out var refreshInterval,
            out var rememberPlacement,
            out var closeToTray,
            out var groupByProject);
        var frameworkToggles = BuildFrameworkRulesSection();
        var blacklist = BuildBlacklistSection();

        var tabHost = new Grid
        {
            MinHeight = 360
        };
        tabHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        tabHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var tabButtons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 14)
        };
        var contentHost = new ContentControl();
        var generalPage = new ScrollViewer
        {
            Content = general,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var rulesPage = new ScrollViewer
        {
            Content = frameworkToggles.View,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var blacklistPage = blacklist.View;

        var generalTab = BuildTabButton("General");
        var rulesTab = BuildTabButton("Rules");
        var blacklistTab = BuildTabButton($"Blacklist ({_excludedPorts.Count})");
        WpfButton[] tabs = [generalTab, rulesTab, blacklistTab];
        generalTab.Click += (_, _) => SelectTab(contentHost, tabs, generalTab, generalPage);
        rulesTab.Click += (_, _) => SelectTab(contentHost, tabs, rulesTab, rulesPage);
        blacklistTab.Click += (_, _) => SelectTab(contentHost, tabs, blacklistTab, blacklistPage);
        tabButtons.Children.Add(generalTab);
        tabButtons.Children.Add(rulesTab);
        tabButtons.Children.Add(blacklistTab);
        Grid.SetRow(tabButtons, 0);
        tabHost.Children.Add(tabButtons);
        Grid.SetRow(contentHost, 1);
        tabHost.Children.Add(contentHost);
        SelectTab(contentHost, tabs, generalTab, generalPage);

        Grid.SetRow(tabHost, 1);
        shell.Children.Add(tabHost);

        var actions = BuildActions(
            refreshInterval,
            showSystemPorts,
            rememberPlacement,
            closeToTray,
            groupByProject,
            frameworkToggles,
            blacklist);
        Grid.SetRow(actions, 2);
        shell.Children.Add(actions);

        return shell;
    }

    private StackPanel BuildGeneralSection(
        out ToggleButton showSystemPorts,
        out WpfComboBox refreshInterval,
        out ToggleButton rememberPlacement,
        out ToggleButton closeToTray,
        out ToggleButton groupByProject)
    {
        var general = new StackPanel
        {
            Margin = new Thickness(0, 18, 0, 0)
        };

        showSystemPorts = new ToggleButton
        {
            IsChecked = _showSystemPorts,
            Style = FindStyle("MaterialDesignSwitchToggleButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        general.Children.Add(BuildSettingRow("System ports", "Include non-development listening ports.", showSystemPorts));

        refreshInterval = new WpfComboBox
        {
            Width = 128,
            SelectedValue = _refreshIntervalSeconds,
            Style = FindStyle("MaterialDesignOutlinedComboBox")
        };
        foreach (var seconds in new[] { 3, 5, 10, 30 })
        {
            refreshInterval.Items.Add(new ComboBoxItem
            {
                Content = $"{seconds} seconds",
                Tag = seconds
            });
        }

        refreshInterval.SelectedIndex = _refreshIntervalSeconds switch
        {
            3 => 0,
            10 => 2,
            30 => 3,
            _ => 1
        };
        general.Children.Add(BuildSettingRow("Refresh interval", "How often PortLens scans in the background.", refreshInterval));

        rememberPlacement = new ToggleButton
        {
            IsChecked = _rememberWindowPlacement,
            Style = FindStyle("MaterialDesignSwitchToggleButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        general.Children.Add(BuildSettingRow("Window placement", "Restore size and position on launch.", rememberPlacement));

        closeToTray = new ToggleButton
        {
            IsChecked = _closeToTray,
            Style = FindStyle("MaterialDesignSwitchToggleButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        general.Children.Add(BuildSettingRow("Close behavior", "Hide to tray when the close button is used.", closeToTray));

        groupByProject = new ToggleButton
        {
            IsChecked = _groupByProject,
            Style = FindStyle("MaterialDesignSwitchToggleButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        general.Children.Add(BuildSettingRow("Group by project", "Group sibling services by inferred project root.", groupByProject));

        general.Children.Add(new TextBlock
        {
            Text = "PortLens - local development port monitor",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            FontSize = 12,
            Margin = new Thickness(0, 18, 0, 0)
        });

        return general;
    }

    private Grid BuildActions(
        WpfComboBox refreshInterval,
        ToggleButton showSystemPorts,
        ToggleButton rememberPlacement,
        ToggleButton closeToTray,
        ToggleButton groupByProject,
        SettingsSection frameworkToggles,
        BlacklistSection blacklist)
    {
        var actions = new Grid
        {
            Margin = new Thickness(0, 18, 0, 0)
        };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var reset = new WpfButton
        {
            Content = "Reset",
            Style = FindStyle("MaterialDesignOutlinedButton"),
            MinWidth = 86,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(179, 38, 30)),
            Command = DialogHost.CloseDialogCommand,
            CommandParameter = new SettingsDialogResult { Reset = true }
        };
        Grid.SetColumn(reset, 0);
        actions.Children.Add(reset);

        var rightActions = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };
        var cancel = new WpfButton
        {
            Content = "Cancel",
            Style = FindStyle("MaterialDesignOutlinedButton"),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 86,
            Command = DialogHost.CloseDialogCommand
        };
        var save = new WpfButton
        {
            Content = "Save",
            Style = FindStyle("MaterialDesignOutlinedButton"),
            MinWidth = 86,
            Command = DialogHost.CloseDialogCommand
        };
        save.Click += (_, _) =>
        {
            var selectedSeconds = refreshInterval.SelectedItem is ComboBoxItem { Tag: int seconds }
                ? seconds
                : 5;
            save.CommandParameter = new SettingsDialogResult
            {
                ShowSystemPorts = showSystemPorts.IsChecked == true,
                RefreshIntervalSeconds = selectedSeconds,
                RememberWindowPlacement = rememberPlacement.IsChecked == true,
                CloseToTray = closeToTray.IsChecked == true,
                GroupByProject = groupByProject.IsChecked == true,
                EnabledFrameworks = frameworkToggles.GetEnabledFrameworks(),
                ExcludedPorts = blacklist.GetExcludedPorts()
            };
        };
        rightActions.Children.Add(cancel);
        rightActions.Children.Add(save);
        Grid.SetColumn(rightActions, 1);
        actions.Children.Add(rightActions);
        return actions;
    }

    private static WpfButton BuildTabButton(string label)
    {
        return new WpfButton
        {
            Content = label,
            Style = FindStyle("MaterialDesignOutlinedButton"),
            MinWidth = 112,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 0, 12, 0)
        };
    }

    private static void SelectTab(ContentControl contentHost, IEnumerable<WpfButton> tabs, WpfButton selected, object content)
    {
        foreach (var tab in tabs)
        {
            tab.Background = System.Windows.Media.Brushes.Transparent;
            tab.Foreground = new SolidColorBrush(WpfColor.FromRgb(103, 58, 183));
        }

        selected.Background = new SolidColorBrush(WpfColor.FromRgb(237, 230, 246));
        selected.Foreground = new SolidColorBrush(WpfColor.FromRgb(54, 30, 97));
        contentHost.Content = content;
    }

    private static Grid BuildSettingRow(string title, string description, UIElement control)
    {
        var row = new Grid
        {
            Margin = new Thickness(0, 0, 0, 14)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel
        {
            Margin = new Thickness(0, 0, 20, 0)
        };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(48, 42, 38))
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var presenter = new ContentControl
        {
            Content = control,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(presenter, 1);
        row.Children.Add(presenter);
        return row;
    }

    private SettingsSection BuildFrameworkRulesSection()
    {
        var toggles = new Dictionary<string, ToggleButton>(StringComparer.OrdinalIgnoreCase);
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 10, 0, 14)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Development rules",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(48, 42, 38)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "These rules decide which recognized services appear when System ports is off.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var grid = new UniformGrid
        {
            Columns = 3
        };
        foreach (var framework in DesktopSettings.DefaultEnabledFrameworks)
        {
            var checkBox = new WpfCheckBox
            {
                Content = framework,
                IsChecked = _enabledFrameworks.Contains(framework),
                Margin = new Thickness(0, 0, 10, 8)
            };
            toggles[framework] = checkBox;
            grid.Children.Add(checkBox);
        }

        panel.Children.Add(grid);
        return new SettingsSection(
            panel,
            () => toggles.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key).ToList());
    }

    private BlacklistSection BuildBlacklistSection()
    {
        var draft = _excludedPorts.Order().ToList();
        var panel = new DockPanel
        {
            Margin = new Thickness(0, 10, 0, 0),
            LastChildFill = true
        };
        var heading = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12)
        };
        heading.Children.Add(new TextBlock
        {
            Text = "Port blacklist",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(48, 42, 38)),
            Margin = new Thickness(0, 0, 0, 4)
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Blacklisted ports stay hidden even when System ports is enabled.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
            FontSize = 12
        });
        DockPanel.SetDock(heading, Dock.Top);
        panel.Children.Add(heading);

        var list = new StackPanel();
        panel.Children.Add(new ScrollViewer
        {
            Content = list,
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        void Render()
        {
            list.Children.Clear();
            if (draft.Count == 0)
            {
                list.Children.Add(new TextBlock
                {
                    Text = "No hidden ports.",
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(124, 113, 106)),
                    FontSize = 12
                });
                return;
            }

            foreach (var port in draft.ToArray())
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
                    FontFamily = new WpfFontFamily("Consolas"),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(63, 123, 200)),
                    VerticalAlignment = VerticalAlignment.Center
                });

                var remove = new WpfButton
                {
                    Content = "Remove",
                    Style = FindStyle("MaterialDesignOutlinedButton"),
                    MinWidth = 76,
                    Height = 28,
                    Padding = new Thickness(8, 0, 8, 0)
                };
                remove.Click += (_, _) =>
                {
                    draft.Remove(port);
                    Render();
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                list.Children.Add(row);
            }
        }

        Render();
        return new BlacklistSection(panel, () => draft.ToList());
    }

    private static Style FindStyle(string key)
    {
        return (Style)System.Windows.Application.Current.FindResource(key);
    }
}
