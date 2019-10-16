using Microsoft.WindowsAPICodePack.Dialogs;
using osu_database_reader.BinaryFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace OsuCollectionPlaycount
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool loading = false;

        private ObservableCollection<Collection> collections = new ObservableCollection<Collection>();
        public IEnumerable<Collection> Collections
        {
            get { return collections; }
        }

        private Collection selectedCollection;
        public Collection SelectedCollection
        {
            get { return selectedCollection; }
            set { selectedCollection = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedCollection")); SortMaps(); }
        }

        public object selectedSorting;
        public object SelectedSorting
        {
            get { return selectedSorting; }
            set
            {
                selectedSorting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedSorting"));
                SortMaps();
            }
        }

        public ICommand ReloadCommand
        {
            get { return new DelegateCommand(() => new Task(LoadCollections).Start()); }
        }

        private string osuPath = null;
        private void GetOsuPath()
        {
            List<string> options = new List<string>();
            options.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu"));
            options.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "osu"));
            options.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "osu"));
            options.Add("C:\\Program Files\\osu");
            options.Add("D:\\Program Files\\osu");
            options.Add("D:\\Program Files (x86)\\osu");

            options.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!"));
            options.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "osu!"));
            options.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "osu!"));
            options.Add("C:\\Program Files\\osu!");
            options.Add("D:\\Program Files\\osu!");
            options.Add("D:\\Program Files (x86)\\osu!");


            foreach (string path in options)
            {
                if (CheckPath(path))
                {
                    osuPath = path;
                    return;
                }
            }

            MessageBox.Show("Could not find osu! directory. Please select it manually");

            var dlg = new CommonOpenFileDialog();
            dlg.Title = "Select osu directory";
            dlg.IsFolderPicker = true;
            //dlg.InitialDirectory = currentDirectory;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            //dlg.DefaultDirectory = currentDirectory;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var folder = dlg.FileName;
                if (CheckPath(folder))
                {
                    osuPath = folder;
                    return;
                }
                else
                    MessageBox.Show("Invalid path");
            }

            Environment.Exit(0);
        }

        private bool CheckPath(string path)
        {
            return File.Exists(Path.Combine(path, "osu!.db"));
        }

        private void LoadCollections()
        {
            if (loading)
                return;
            loading = true;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                collections.Clear();
                collections.Add(new Collection("loading...", new TimeSpan()));
            }
            );


            //string path = @"D:\Program Files\osu";

            CollectionDb collectionsdb = CollectionDb.Read(Path.Combine(osuPath, "collection.db"));
            ScoresDb scores = ScoresDb.Read(Path.Combine(osuPath, "scores.db"));
            OsuDb maps = OsuDb.Read(Path.Combine(osuPath, "osu!.db"));

            System.Windows.Application.Current.Dispatcher.Invoke(() => collections.Clear());
            foreach (var coll in collectionsdb.Collections)
            {
                ObservableCollection<Map> loadedMaps = new ObservableCollection<Map>();
                int totalPlayTime = 0;

                foreach (string hash in coll.BeatmapHashes)
                {
                    int playCount;
                    if (scores.Beatmaps.ContainsKey(hash))
                        playCount = scores.Beatmaps[hash].Count;
                    else
                        playCount = 0;

                    var map = maps.Beatmaps.Where((a) => a.BeatmapChecksum == hash).FirstOrDefault();
                    if (map == null)
                        continue;
                    int length = map.DrainTimeSeconds;

                    double stars = -1;
                    if (map.DiffStarRatingStandard.Count > 0)
                        stars = map.DiffStarRatingStandard[osu.Shared.Mods.None];
                    else if (map.DiffStarRatingCtB.Count > 0)
                        stars = map.DiffStarRatingCtB[osu.Shared.Mods.None];
                    else if (map.DiffStarRatingMania.Count > 0)
                        stars = map.DiffStarRatingMania[osu.Shared.Mods.None];
                    else if (map.DiffStarRatingTaiko.Count > 0)
                        stars = map.DiffStarRatingTaiko[osu.Shared.Mods.None];

                    var loadedMap = new Map(map.Title + ' ' + '[' + map.Version + ']', stars, TimeSpan.FromSeconds(length * playCount));
                    loadedMaps.Add(loadedMap);
                    totalPlayTime += length * playCount;
                }

                Collection loaded = new Collection(coll.Name, TimeSpan.FromSeconds(totalPlayTime));
                loaded.Maps = loadedMaps;
                System.Windows.Application.Current.Dispatcher.Invoke(() => collections.Add(loaded));
            }
            loading = false;
        }

        private void SortMaps()
        {
            if (SelectedCollection == null)
                return;
            switch ((selectedSorting as TextBlock).Text)
            {
                case "By difficulty":
                    SelectedCollection.Maps = new ObservableCollection<Map>(SelectedCollection.Maps.OrderBy((a) => a.starRating));
                    break;
                case "By title":
                    SelectedCollection.Maps = new ObservableCollection<Map>(SelectedCollection.Maps.OrderBy((a) => a.Name));
                    break;
                case "By playtime":
                    SelectedCollection.Maps = new ObservableCollection<Map>(SelectedCollection.Maps.OrderBy((a) => -a.PlaytimeMs));
                    break;
            }
        }

        public ViewModel()
        {
            /*
            Collection test = new Collection("tech", new TimeSpan(10, 5, 11));
            Collection test1 = new Collection("streams", new TimeSpan(1, 1, 1, 1, 555));

            test.Maps.Add(new Map("future candy", 1.1, new TimeSpan(123, 11, 56)));
            test.Maps.Add(new Map("future cider", 0.5, new TimeSpan(45, 45, 45)));

            test1.Maps.Add(new Map("180 bpm", 77.1, new TimeSpan(450, 30, 11)));
            test1.Maps.Add(new Map("dragonforce", 75, new TimeSpan(0, 0, 0)));

            collections.Add(test);
            collections.Add(test1);*/

            GetOsuPath();
            var task = new Task(LoadCollections);
            task.Start();
        }
    }

    public class Collection : INotifyPropertyChanged
    {
        private TimeSpan playtime;
        private string name;
        private ObservableCollection<Map> maps = new ObservableCollection<Map>();

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get { return name; }
            set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name")); }
        }

        public string Playtime
        {
            get
            {
                string result = (int)playtime.TotalHours > 0 ? ((int)(playtime.TotalHours)).ToString() + " h " : "";
                result += playtime.Minutes > 0 ? playtime.Minutes.ToString() + " m " : "";
                result += playtime.Seconds.ToString() + " s";
                return result;
            }
        }

        public ObservableCollection<Map> Maps
        {
            get { return maps; }
            set { maps = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Maps")); }
        }

        public Collection(string name, TimeSpan playtime)
        {
            this.Name = name;
            this.playtime = playtime;
        }
    }

    public class DelegateCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private Action del;
        public DelegateCommand(Action del)
        {
            this.del = del;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            del.Invoke();
        }
    }

    public class Map : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private TimeSpan playtime;
        private string name;
        public readonly double starRating;

        public string Name
        {
            get { return name; }
            set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name")); }
        }
        public string Playtime
        {
            get
            {
                string result = playtime.Hours > 0 ? playtime.Hours.ToString() + " h " : "";
                result += playtime.Minutes > 0 ? playtime.Minutes.ToString() + " m " : "";
                result += playtime.Seconds.ToString() + " s";
                return result;
            }
        }
        public long PlaytimeMs
        {
            get
            {
                return (long)playtime.TotalMilliseconds;
            }
        }

        public Map(string name, double starRating, TimeSpan playtime)
        {
            this.Name = name;
            this.playtime = playtime;
            this.starRating = starRating;
        }
    }
}
