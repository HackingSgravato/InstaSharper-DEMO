using Avalonia.Controls;
using InstaSharper.API;
using InstaSharper.API.Builder;
using InstaSharper.Classes;
using InstaSharper.Logger;
using Avalonia.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

namespace InstaSharper_DEMO
{
    public partial class MainWindow : Window
    {
        private IInstaApi _instaApi;

        string username;
        string password;

        List<User> users;
        List<long> blacklistedUserIDs;
        IResult<InstaSharper.Classes.Models.InstaCurrentUser> currentUser;
        IResult<InstaSharper.Classes.Models.InstaUserShortList> following;

        int countUnfollows;

        public MainWindow()
        {
            InitializeComponent();

            MyLoginButton.Click += MyLoginButton_Click;
            MyDeselectionButton.Click += MyDeselectionButton_Click;
            MyUnfollowButton.Click += MyUnfollowButton_Click;
            MyFinderButton.Click += MyFinderButton_Click;
            MyBackButton.Click += MyBackButton_Click;
            MyShowPasswordButton.Click += MyShowPasswordButton_Click;
            MyRefreshFollowersButton.Click += MyRefreshFollowersButton_Click;

            countUnfollows = 0;
        }

        private void MyRefreshFollowersButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            MyRefreshFollowersButton.Content = "Aggiornando";
            MyRefreshFollowersButton.IsEnabled = false;

            resetCounter();
            refreshFollowers();
            EnumerateFollowers();

            MyRefreshFollowersButton.Content = "Reimposta la lista dei followers";
            MyRefreshFollowersButton.IsEnabled = true;
        }

        int counter = 0;

        void resetCounter()
        {
            counter = 0;

            MyCounterLabel.Content = "Utenti deselezionati: non definito";
        }

        void increaseCounter()
        {
            MyCounterLabel.Content = $"Utenti deselezionati: { ++counter }";
        }

        void refreshFollowers()
        {
            users = new();
            blacklistedUserIDs = new();

            Parallel.ForEach(following.Value, user =>
            {
                try
                {
                    users.Add(new User
                    {
                        UserID = user.Pk,
                        UserName = user.UserName
                    });
                }
                catch { }
            });
        }
        
        void EnumerateFollowers()
        {
            Dispatcher.UIThread.Post(() =>
            {
                MyListBox.Items = users.Where(a => !blacklistedUserIDs.Contains(a.UserID)).Select(a => a.InformationsDisplay);
            });
        }

        private void MyShowPasswordButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool passwordVisibilityState = TextBoxPass.RevealPassword;

            switch (passwordVisibilityState)
            {
                case true:
                    TextBoxPass.RevealPassword = false;
                    MyShowPasswordButton.Content = "Mostra password";
                    break;
                default:
                    TextBoxPass.RevealPassword = true;
                    MyShowPasswordButton.Content = "Nascondi password";
                    break;
            }
        }

        private void MyBackButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            EnumerateFollowers();
        }

        private void MyFinderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string text = MyTextBox.Text;

            Dispatcher.UIThread.Post(() =>
            {
                MyListBox.Items = users.Where(a => a.UserName.ToLower().Contains(text.ToLower()) && !blacklistedUserIDs.Contains(a.UserID)).Select(a => a.InformationsDisplay);
            });
        }

        private void MyUnfollowButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            MyUnfollowButton.Content = "Togliendo il follow";
            MyUnfollowButton.IsEnabled = false;

            bool limiter = (bool)MyLimiterCheckBox.IsChecked;

            if (limiter)
            {
                Parallel.ForEach(users, user =>
                {
                    if (!blacklistedUserIDs.Contains(user.UserID))
                    {
                        _instaApi.UnFollowUserAsync(user.UserID);
                        if (++countUnfollows == 200)
                        {
                            MyUnfollowButton.Content = "Hai superato il limite giornaliero di unfollow consigliato per non essere limitato. Torna fra 24 ore.";

                            return;
                        }
                    }
                });
            }
            else
            {
                Parallel.ForEach(users, user =>
                {
                    if (!blacklistedUserIDs.Contains(user.UserID))
                    {
                        _instaApi.UnFollowUserAsync(user.UserID);
                    }
                });
            }


            MyUnfollowButton.Content = "Togli il follow alle persone presenti nella lista";
            MyUnfollowButton.IsEnabled = true;
        }

        private void MyDeselectionButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string text = (string)MyListBox.SelectedItem;

            Regex r = new Regex(@"[(].+[)]");

            long userId = Convert.ToInt64(r.Match(text).Value.Replace("(", string.Empty).Replace(")", string.Empty));

            blacklistedUserIDs.Add(userId);

            EnumerateFollowers();

            increaseCounter();
        }

        private async void MyLoginButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            MyLoginButton.IsEnabled = false;
            MyLoginButton.Content = "Eseguendo l'accesso";

            refreshCredentials();

            try
            {
                var userSession = new UserSessionData
                {
                    UserName = username,
                    Password = password
                };

                var delay = RequestDelay.FromSeconds(2, 2);
                _instaApi = InstaApiBuilder.CreateBuilder()
                    .SetUser(userSession)
                    .UseLogger(new DebugLogger(LogLevel.Exceptions))
                    .SetRequestDelay(delay)
                    .Build();

                var loginRequest = await _instaApi.LoginAsync();
                if (loginRequest.Succeeded)
                {
                    loginSucceeded();

                    Title += $"     Accesso come {username}";
                }
                else { loginError(); return; }
            }
            catch
            {
                loginError();
                return;
            }

            MyLoginButton.Content = "Connettendo all'account";
            currentUser = await _instaApi.GetCurrentUserAsync();
            long userId = currentUser.Value.Pk;
            MyUserIDLabel.Content = $"ID utente: { userId }";


            MyLoginButton.Content = "Recuperando i followers";
            following = await _instaApi.GetUserFollowingAsync(currentUser.Value.UserName, PaginationParameters.MaxPagesToLoad(20));


            MyLoginButton.Content = "Login";
            MyLoginButton.IsEnabled = true;
        }

        void refreshCredentials()
        {
            username = TextBoxUser.Text;
            password = TextBoxPass.Text;
        }

        void loginError()
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                MyLabel.Content = "Qualcosa è andato storto.";
                MyLabel.IsVisible = true;

                await Task.Delay(3000);
                MyLabel.IsVisible = false;
            });
        }

        void loginSucceeded()
        {
            Dispatcher.UIThread.Post(async () =>
            {
                MyLabel.Content = "Accesso avvenuto con successo.";
                MyLabel.IsVisible = true;

                await Task.Delay(3000);
                MyLabel.IsVisible = false;
            });
        }
    }

    struct User
    {
        public long UserID { get; set; }
        public string UserName { get; set; }

        public string InformationsDisplay
        {
            get => $"({ UserID }) {UserName}";
        }
    }
}
