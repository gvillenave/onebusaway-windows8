﻿using Bing.Maps;
using OneBusAway.Controls;
using OneBusAway.DataAccess;
using OneBusAway.Model;
using OneBusAway.Utilities;
using OneBusAway.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace OneBusAway.Pages
{
    /// <summary>
    /// Main Page of the OBA app
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainPageViewModel mainPageViewModel;

        public MainPage()
        {
            this.InitializeComponent();

            this.mainPageViewModel = (MainPageViewModel)this.DataContext;           

        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (NavigationController.Instance.PersistedStates.Count > 0 && e.NavigationMode == NavigationMode.Back)
            {
                Dictionary<string, object> previousState = NavigationController.Instance.PersistedStates.Pop();
                mainPageViewModel.MapControlViewModel = (MapControlViewModel)previousState["mapControlViewModel"];
                mainPageViewModel.RoutesAndStopsViewModel = (RoutesAndStopsControlViewModel)previousState["routesAndStopsControlViewModel"];
                mainPageViewModel.HeaderViewModel = (HeaderControlViewModel)previousState["headerViewModel"];
            }
            else 
            {
                Geolocator geolocator = new Geolocator();
                var position = await geolocator.GetGeopositionAsync();

                OneBusAway.Model.Point userLocation = new OneBusAway.Model.Point(position.Coordinate.Latitude, position.Coordinate.Longitude);
                mainPageViewModel.MapControlViewModel.UserLocation = userLocation;

                mainPageViewModel.MapControlViewModel.MapView = new MapView(userLocation, ViewModelConstants.DefaultMapZoom);

                // Load favorites:
                await this.mainPageViewModel.RoutesAndStopsViewModel.PopulateFavoritesAsync();
            }

            base.OnNavigatedTo(e);
        }

        /// <summary>
        /// Invoked when the user navigates from this page.
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Persist the state for later:
            if (e.NavigationMode != NavigationMode.Back)
            {
                NavigationController.Instance.PersistedStates.Push(new Dictionary<string, object>()
                {
                    {"mapControlViewModel", this.mainPageViewModel.MapControlViewModel},
                    {"routesAndStopsControlViewModel", this.mainPageViewModel.RoutesAndStopsViewModel},
                    {"headerViewModel", this.mainPageViewModel.HeaderViewModel}
                });
            }

            base.OnNavigatedFrom(e);
        }
    }
}
