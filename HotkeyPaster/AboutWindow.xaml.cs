using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using TalkKeys.Logging;
using TalkKeys.Services.About;

namespace TalkKeys
{
    public enum AboutWindowPage
    {
        About,
        WhatsNew
    }

    public partial class AboutWindow : Window
    {
        private readonly AboutContentService _contentService;
        private readonly ILogger _logger;
        private AboutContent? _content;
        private ReleaseInfo? _currentRelease;
        private int _currentSlide;
        private double _slideWidth;
        private readonly List<RadioButton> _dots = new();

        public AboutWindow(ILogger logger, AboutWindowPage defaultPage = AboutWindowPage.About)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _contentService = new AboutContentService(logger);

            // Set version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }

            // Set default page
            if (defaultPage == AboutWindowPage.WhatsNew)
            {
                NavWhatsNew.IsChecked = true;
                NavAbout.IsChecked = false;
            }

            Loaded += AboutWindow_Loaded;
        }

        private async void AboutWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Calculate slide width based on available space
            _slideWidth = WhatsNewPage.ActualWidth > 0 ? WhatsNewPage.ActualWidth - 64 : 500;

            // Show loading
            LoadingIndicator.Visibility = Visibility.Visible;

            try
            {
                // Load content from backend
                _content = await _contentService.GetContentAsync();
                PopulateContent();
            }
            catch (Exception ex)
            {
                _logger.Log($"[AboutWindow] Error loading content: {ex.Message}");
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }

            // Show appropriate page
            UpdatePageVisibility();
        }

        private void PopulateContent()
        {
            if (_content == null) return;

            // About page content
            TaglineText.Text = _content.Tagline;
            MadeWithLoveText.Text = _content.MadeWithLove;
            LibrariesList.ItemsSource = _content.Libraries;
            LinksList.ItemsSource = _content.Links;

            // Load the latest release for What's New
            var latestRelease = _content.Releases.FirstOrDefault();
            if (latestRelease != null)
            {
                _currentRelease = latestRelease;
                ReleaseVersionText.Text = $"v{latestRelease.Version} - {latestRelease.Title}";
                BuildCarousel(latestRelease);
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            UpdatePageVisibility();
        }

        private void UpdatePageVisibility()
        {
            var showWhatsNew = NavWhatsNew.IsChecked == true;
            AboutPage.Visibility = showWhatsNew ? Visibility.Collapsed : Visibility.Visible;
            WhatsNewPage.Visibility = showWhatsNew ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BuildCarousel(ReleaseInfo release)
        {
            SlidePanel.Children.Clear();
            DotsPanel.Children.Clear();
            HeroFeaturesPanel.Children.Clear();
            _dots.Clear();
            _currentSlide = 0;

            // Reset slide position to first slide
            SlideTransform.X = 0;

            // Build hero features if present
            if (release.HeroFeatures.Count > 0)
            {
                HeroFeaturesPanel.Visibility = Visibility.Visible;
                foreach (var hero in release.HeroFeatures)
                {
                    var heroCard = CreateHeroFeatureCard(hero);
                    HeroFeaturesPanel.Children.Add(heroCard);
                }
            }
            else
            {
                HeroFeaturesPanel.Visibility = Visibility.Collapsed;
            }

            // Calculate actual slide width
            _slideWidth = Math.Max(WhatsNewPage.ActualWidth - 64, 400);

            foreach (var (slide, index) in release.Slides.Select((s, i) => (s, i)))
            {
                var slideElement = CreateSlideElement(slide);
                SlidePanel.Children.Add(slideElement);

                // Create dot
                var dot = new RadioButton
                {
                    Style = (Style)FindResource("NavDotStyle"),
                    Tag = index,
                    IsChecked = index == 0
                };
                dot.Click += Dot_Click;
                DotsPanel.Children.Add(dot);
                _dots.Add(dot);
            }

            UpdateNavigationState();
        }

        private Border CreateHeroFeatureCard(HeroFeatureInfo hero)
        {
            var baseColor = (Color)ColorConverter.ConvertFromString(hero.Color);
            var bgColor = Color.FromArgb(25, baseColor.R, baseColor.G, baseColor.B);   // ~10% opacity
            var borderColor = Color.FromArgb(60, baseColor.R, baseColor.G, baseColor.B); // ~24% opacity

            var card = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(5, 0, 5, 0),
                Width = 200
            };

            var stack = new StackPanel();

            // Icon and title row
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            headerStack.Children.Add(new TextBlock
            {
                Text = hero.Icon,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = hero.Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hero.Color)),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(headerStack);

            // Description
            stack.Children.Add(new TextBlock
            {
                Text = hero.Description,
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Badge if present
            if (!string.IsNullOrEmpty(hero.Badge))
            {
                var badgeBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hero.Color)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 3, 6, 3),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                badgeBorder.Child = new TextBlock
                {
                    Text = hero.Badge,
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    Foreground = Brushes.White
                };
                stack.Children.Add(badgeBorder);
            }

            card.Child = stack;
            return card;
        }

        private Border CreateSlideElement(SlideInfo slide)
        {
            var container = new Border
            {
                Width = _slideWidth,
                Padding = new Thickness(16, 0, 16, 0)
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            // Icon
            var iconBorder = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(28),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(slide.IconBackground)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            var iconText = new TextBlock
            {
                Text = slide.Icon,
                FontSize = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
            stack.Children.Add(iconBorder);

            // Title
            var title = new TextBlock
            {
                Text = slide.Title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(title);

            // Description
            var desc = new TextBlock
            {
                Text = slide.Description,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 320
            };
            stack.Children.Add(desc);

            // Badge (if present)
            if (slide.Badge != null)
            {
                var badgeContainer = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 14, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var badgeStack = new StackPanel { Orientation = Orientation.Horizontal };
                badgeStack.Children.Add(new TextBlock
                {
                    Text = slide.Badge.Label,
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                    VerticalAlignment = VerticalAlignment.Center
                });

                var valueBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(slide.Badge.BackgroundColor)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(6, 0, 0, 0)
                };
                valueBorder.Child = new TextBlock
                {
                    Text = slide.Badge.Value,
                    FontSize = 10,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Medium
                };
                badgeStack.Children.Add(valueBorder);

                badgeContainer.Child = badgeStack;
                stack.Children.Add(badgeContainer);
            }

            // Highlights (if present)
            if (slide.Highlights.Count > 0)
            {
                var highlightBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(slide.IconBackground)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 14, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var highlightStack = new StackPanel();
                foreach (var highlight in slide.Highlights)
                {
                    highlightStack.Children.Add(new TextBlock
                    {
                        Text = $"\u2713 {highlight.Text}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(highlight.Color)),
                        Margin = new Thickness(0, 1, 0, 1)
                    });
                }

                highlightBorder.Child = highlightStack;
                stack.Children.Add(highlightBorder);
            }

            // Get Started button (if applicable)
            if (slide.IsGetStarted)
            {
                var buttonStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 18, 0, 0)
                };

                var getStartedButton = new Button
                {
                    Content = "Get Started",
                    Style = (Style)FindResource("PrimaryButtonStyle")
                };
                getStartedButton.Click += (s, e) => Close();
                buttonStack.Children.Add(getStartedButton);
                stack.Children.Add(buttonStack);
            }

            container.Child = stack;
            return container;
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSlide(_currentSlide - 1);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSlide(_currentSlide + 1);
        }

        private void Dot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton dot && dot.Tag is int index)
            {
                NavigateToSlide(index);
            }
        }

        private void NavigateToSlide(int slideIndex)
        {
            if (_currentRelease == null) return;

            slideIndex = Math.Max(0, Math.Min(slideIndex, _currentRelease.Slides.Count - 1));

            if (slideIndex == _currentSlide) return;

            _currentSlide = slideIndex;

            // Animate
            var targetX = -slideIndex * _slideWidth;
            var animation = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            SlideTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            // Update dots
            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].IsChecked = (i == slideIndex);
            }

            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            if (_currentRelease == null) return;

            PrevButton.IsEnabled = _currentSlide > 0;
            NextButton.IsEnabled = _currentSlide < _currentRelease.Slides.Count - 1;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently ignore if browser can't be opened
            }
        }
    }
}
