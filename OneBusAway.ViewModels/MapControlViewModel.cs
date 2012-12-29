﻿using OneBusAway.DataAccess;
using OneBusAway.Model;
using OneBusAway.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneBusAway.ViewModels
{
    /// <summary>
    /// View model for the map control
    /// </summary>
    public class MapControlViewModel : ViewModelBase
    {
        private OneBusAway.Model.Point userLocation;
        private MapView mapView;
        private List<Stop> busStops;
        private List<Shape> shapes;
        private bool refreshBusStopsOnMapViewChanged;

        public event EventHandler<StopSelectedEventArgs> StopSelected;

        /// <summary>
        /// Creates the map control view model.
        /// </summary>
        public MapControlViewModel()
        {
            this.mapView = MapView.Current;
            this.RefreshBusStopsOnMapViewChanged = true;
        }

        /// <summary>
        /// When true, the control will refresh bus stops when the map moves. When false, it will use a static list.
        /// </summary>
        public bool RefreshBusStopsOnMapViewChanged
        {
            get
            {
                return this.refreshBusStopsOnMapViewChanged;
            }
            set
            {
                SetProperty(ref this.refreshBusStopsOnMapViewChanged, value);
            }
        }

        /// <summary>
        /// Gets an object that describes the state of the map at any point of time. This property should be bound to the MapControl's MapView property in xaml.
        /// This property is updated whenever the map view changes (either zoom or the center of the map)
        /// </summary>
        public MapView MapView
        {
            get
            {
                return mapView;
            }
            set
            {
                SetProperty(ref mapView, value);
                MapView.Current = value;

                if (this.RefreshBusStopsOnMapViewChanged && value.ZoomLevel > UtilitiesConstants.MinBusStopVisibleZoom)
                {
                    RefreshStopsForLocationAsync();
                }
            }
        }

        public List<Stop> BusStops
        {
            get
            {
                return busStops;
            }
            set
            {
                SetProperty(ref busStops, value);
            }
        }

        public List<Shape> Shapes
        {
            get
            {
                return this.shapes;
            }
            set
            {
                SetProperty(ref this.shapes, value);
            }
        }

        public OneBusAway.Model.Point UserLocation
        {
            get
            {
                return userLocation;
            }
            set
            {
                SetProperty(ref this.userLocation, value);
            }
        }

        public async void RefreshStopsForLocationAsync()
        {
            try
            {
                var output = await new ObaDataAccess().GetStopsForLocationAsync(mapView.MapCenter.Latitude, mapView.MapCenter.Longitude, mapView.BoundsHeight, mapView.BoundsWidth);

                BusStops = output.ToList();
            }
            catch (Exception)
            {
                // TODO
            }
        }
        
        /// <summary>
        /// Called when a stop is selected.
        /// </summary>
        public void SelectStop(string name, string selectedStopId, string direction)
        {
            var stopSelected = this.StopSelected;
            if (stopSelected != null)
            {
                stopSelected(this, new StopSelectedEventArgs(name, selectedStopId, direction));
            }
        }
    }
}
