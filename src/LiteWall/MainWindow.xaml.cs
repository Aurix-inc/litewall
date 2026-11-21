using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LiteWall
{
    public class WallpaperItem
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string FilePath { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private readonly WallpaperWindow _wallpaperWindow;

        public MainWindow()
        {
            InitializeComponent();
            _wallpaperWindow = new WallpaperWindow();
            _wallpaperWindow.Show();
            LoadPrebuiltWallpapers();
        }

        private void LoadPrebuiltWallpapers()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wallpapers");
            var items = new List<WallpaperItem>
            {
                new WallpaperItem { Name = "Matrix Digital Rain", Type = "HTML5 Canvas • 0% GPU", FilePath = Path.Combine(baseDir, "matrix.html") },
                new WallpaperItem { Name = "Pomodoro Focus Timer", Type = "Interactive Widget • Clickable", FilePath = Path.Combine(baseDir, "pomodoro.html") },
                new WallpaperItem { Name = "Constellation Nodes", Type = "Interactive Web • 60 FPS", FilePath = Path.Combine(baseDir, "particles.html") },
                new WallpaperItem { Name = "Aurora Gradient", Type = "Pure CSS Shader", FilePath = Path.Combine(baseDir, "aurora.html") }
            };

            PrebuiltList.ItemsSource = items;
            if (File.Exists(items[0].FilePath))
            {
                _wallpaperWindow.LoadFile(items[0].FilePath);
            }
        }

        private async void CopyPromptBtn_Click(object sender, RoutedEventArgs e)
        {
            string prompt = @"Create a single-file HTML5 live wallpaper for a Windows desktop wallpaper app. Put all CSS inside <style> and all JavaScript inside <script> (no external files).
What I want on my wallpaper: [DESCRIBE YOUR IDEA HERE, e.g., a live clock with a sticky note to-do list]
Rules:
1. Set body to margin: 0; overflow: hidden; width: 100vw; height: 100vh;
2. Place clickable buttons near the center of the screen and use standard onclick events.
3. Inside any animation loop, add: if (window.__paused) return;
4. Use localStorage to save user data like checklists or notes.";

            Clipboard.SetText(prompt);
            CopyPromptBtn.Content = "✓ Copied! Paste in AI";
            await Task.Delay(2500);
            CopyPromptBtn.Content = "✨ Copy AI Prompt";
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) _wallpaperWindow.LoadFile(files[0]);
            }
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "All Supported|*.html;*.htm;*.mp4;*.webm;*.gif;*.png;*.jpg|All Files|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _wallpaperWindow.LoadFile(dlg.FileName);
            }
        }

        private void PrebuiltList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PrebuiltList.SelectedItem is WallpaperItem item)
            {
                _wallpaperWindow.LoadFile(item.FilePath);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _wallpaperWindow.Close();
            Application.Current.Shutdown();
        }
    }
}
