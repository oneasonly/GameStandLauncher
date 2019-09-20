﻿using Logic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace WPF
{
    public partial class MainWindow : Window
    {
        //MouseHookAdapter mh;
        //bool isShowTaskbarOnExit = true;
        //readonly bool isRelaunched;
        bool isClickable = true;
        public MainWindow()
        {            

        }

        public MainWindow(bool relaunched):base()
        {
            InitializeComponent();
            SystemManager.isRelaunched = false;
#if DEBUG
            WindowState = WindowState.Normal;
#else
            WindowState = WindowState.Maximized;
#endif
            SystemManager.isRelaunched = relaunched;
            Ex.Log($"argumented ctor isRelaunched={SystemManager.isRelaunched}");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SystemManager.OnWindowLoaded();
            ReloadGrid();
            SetEventSubscribes();
            SetBackground();
            SetHeadline();            
        }

        private void SetEventSubscribes()
        {
            SystemManager.OnBeforeRelaunchApp += DisableUI;
        }

        private async void button1_Click(object sender, RoutedEventArgs e)
        {
            //===TEST BUTTON===
            //RelaunchApp();
            await Task.Delay(5000);
            DisableUI();
            //await Task.Delay(90000);
            //SystemManager.WakeMonitor();
        }

        private void SetHeadline()
        {
            Ex.Log("SetHeadline() called.");
            try
            {
                var local = new SavingManager(Where.local);
                label1.Content = local.Key(Setting.HeadlineLauncherText).Value;
                local.Save();
            }
            catch (Exception ex)
            {
                ex.Log("Error at SetHeadline()");
            }
        }

        private void SetBackground()
        {
            Ex.Log("SetBackground(): called");
            try
            {
                if (File.Exists("background.png"))
                {
                    Ex.Log("SetBackground(): background.png exists (найден).");
                    ImageBrush myBrush = new ImageBrush();
                    var img = new BitmapImage(new Uri("background.png", UriKind.Relative));
                    try
                    {
                        var temp = img.Format;
                        myBrush.ImageSource = img;
                        this.Background = myBrush;
                    }
                    catch(Exception ex)
                    {
                        ex.Log("SetBackground()");
                    }
                }
                else
                {
                    Ex.Log("SetBackground(): background.png not found.");
                }
            }
            catch (Exception ex)
            {
                ex.Log("SetBackground()");
            }
        }

        private static void Show(string msg)
        {
            MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            ReloadGrid();
        }
        private void TestImageControl()
        {
            string pathImg = @"D:\Work Horizont\img\Ph03nyx-Super-Mario-Mushroom-1UP.ico";
            Image imgControl = ImageCreateGameIcon(pathImg);
            Grid.SetRow(imgControl, 4);
            Grid.SetColumn(imgControl, 1);
            gamesGrid.Children.Add(imgControl);
        }
        private Image ImageCreateGameIcon(string incPathGame)
        {
            var imgControl = new Image();
            ImageSource imageSource = null;
            imageSource = GameManager.FindLocalImg(incPathGame);

            try
            {
                var realExePath = GameManager.GetShortcutTarget(incPathGame);
                imgControl.Source = imageSource ?? (realExePath == null ? Properties.Resources.game2.ToImageSource() : GameManager.GetHighResIcon(realExePath).ToImageSource() );
            }
            catch(Exception ex)
            {
                ex.Log($"Error at ImageCreateGameIcon(string incPathGame={incPathGame})\n{ex.Message}");
                Show($"Ошибка с ярлыком {incPathGame};\n\n{ex.Message}\n\n MainWindow.ImageCreateGameIcon()");
            }
            imgControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            imgControl.VerticalAlignment = VerticalAlignment.Stretch;
            return imgControl;
        }
        private TextBlock LabelCreateGameName(string incPathGame)
        {
            var box = new TextBlock();
            var nameFile = new FileInfo(incPathGame).Name;
            box.Text = GameManager.CleanAfterDot(nameFile);

            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Stretch;
            box.TextAlignment = TextAlignment.Center;
            box.TextWrapping = TextWrapping.Wrap;

            //box.HorizontalContentAlignment = HorizontalAlignment.Center;
            //box.VerticalContentAlignment = VerticalAlignment.Top;
            box.FontSize = 20;            
            box.FontFamily = new FontFamily("Arial");            
            return box;
        }
        private void ReloadGrid()
        {
            Ex.Log("ReloadGrid(): called.");
            ClearGrid();
            FillGrid();
        }
        private void CreateFilledCell(int i, int j, string content)
        {
            var grid = new gridCellUser();

            Image imgControl = ImageCreateGameIcon(content);
            imgControl.Loaded += async (s, e) =>
            {
                var rnd = new Random(i * 10 + j);
                while (true)
                {
                    var interval = rnd.Next(5, 30);
                    await Task.Delay(interval * 1000);
                    var top = rnd.Next(5, 30);
                    var left = rnd.Next(5, 40);
                    var animationTime = rnd.Next(3, 10);
                    var leftRight = rnd.Next(1, 3);
                    int right = leftRight == 1 ? 0 : left;
                    left = right == 0 ? left : 0;
                    ThicknessAnimation anim = RandomMarginAnimation(left, top, right, animationTime * 100);
                    //Timeline.SetDesiredFrameRate(anim, 30); // 60 FPS
                    imgControl.BeginAnimation(MarginProperty, anim);
                }
            };
            Grid.SetRow(imgControl, 0);

            var lblControl = LabelCreateGameName(content);
            Grid.SetRow(lblControl, 2);

            grid.contentGrid.Children.Add(imgControl);
            grid.contentGrid.Children.Add(lblControl);

            ThicknessAnimation marginAnimation = MarginAnimation();
            DoubleAnimation fontAnimation = FontAnimation();

            grid.contentGrid.MouseDown += async (o, e) =>
            {
                if (isClickable)
                {
                    isClickable = false;
                    var task = GameManager.RunGame(content);
                    grid.contentGrid.BeginAnimation(MarginProperty, marginAnimation);
                    lblControl.BeginAnimation(Label.FontSizeProperty, fontAnimation);
                    try
                    {
                        await task;
                        await Task.Delay(1000);
                        isClickable = true;
                    }
                    catch (Exception ex)
                    {
                        Show(ex.Message);
                        isClickable = true;
                    }
                }
            };
            grid.contentGrid.TouchDown += async (o, e) =>
            {
                if (isClickable)
                {
                    isClickable = false;
                    var task = GameManager.RunGame(content);
                    grid.contentGrid.BeginAnimation(MarginProperty, marginAnimation);
                    lblControl.BeginAnimation(Label.FontSizeProperty, fontAnimation);
                    try
                    {
                        await task;
                        await Task.Delay(1000);
                        isClickable = true;
                    }
                    catch (Exception ex)
                    {
                        Show(ex.Message);
                        isClickable = true;
                    }
                }
            };

            Grid.SetRow(grid, i);
            Grid.SetColumn(grid, j);
            gamesGrid.Children.Add(grid);
        }
        private static ThicknessAnimation RandomMarginAnimation(double left, double top, double right, double length)
        {
            var animation = new ThicknessAnimation();
            animation.From = new Thickness(0);
            animation.To = new Thickness(left, top, right, 0);
            animation.Duration = TimeSpan.FromMilliseconds(length);
            animation.AutoReverse = true;
            var ease = new SineEase();
            ease.EasingMode = EasingMode.EaseInOut;
            animation.EasingFunction = ease;
            return animation;
        }
        private static ThicknessAnimation MarginAnimation()
        {
            var animation = new ThicknessAnimation();
            //animation.From = new Thickness(40);
            animation.To = new Thickness(40);
            animation.Duration = TimeSpan.FromMilliseconds(150);
            animation.AutoReverse = true;
            var ease = new CircleEase();
            //ease.Power = 3;
            ease.EasingMode = EasingMode.EaseOut;
            //animation.EasingFunction = ease;
            return animation;
        }
        private static DoubleAnimation FontAnimation()
        {
            var animation = new DoubleAnimation();
            //animation.From = 6;
            animation.To = 18;
            animation.Duration = TimeSpan.FromMilliseconds(100);
            animation.AutoReverse = true;
            var ease = new CircleEase();
            //ease.Power = 3;
            ease.EasingMode = EasingMode.EaseIn;
            animation.EasingFunction = ease;
            return animation;
        }
        private void ControlsCircle(int i, int j, string content)
        {
            var ControlsList = new List<FrameworkElement>();
            ControlsList.Add(ImageCreateGameIcon(content));
            ControlsList.Add(LabelCreateGameName(content));

            foreach (var item in ControlsList)
            {
                Grid.SetRow(item, i);
                Grid.SetColumn(item, j);
                gamesGrid.Children.Add(item);
            }
        }
        private void FillGrid()
        {
            string[] listGames = null;
            try
            {
                listGames = GameManager.GetAllGames();
            }
            catch (Exception ex)
            {
                ex.Log("Error at 'GameManager.GetAllGames()'");
                Show($"Error at 'GameManager.GetAllGames()'\n{ex.Message}");
            }
            int numGame = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    if (numGame >= listGames.Length)
                    { break; }
                    CreateFilledCell(i, j, listGames[numGame]);
                    numGame++;
                }
            }


        }
        private void ClearGrid()
        {
            gamesGrid.Children.Clear();
        }
        private void imgTest_MouseUp(object sender, MouseButtonEventArgs e)
        {
        }        
        private void ImgTest_TouchDown(object sender, TouchEventArgs e)
        {

        }
        private void button2_Click(object sender, RoutedEventArgs e)
        {
            GameManager.boolTest = true;
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            GameManager.boolTest = false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            SystemManager.OnWindowClosed();
        }

        private void DisableUI()
        {
            Ex.Log("DisableUI(): called.");
            gamesGrid.IsEnabled = false;
            label1.Content = "Пожалуйста подождите / Please Wait";
        }
    }
}