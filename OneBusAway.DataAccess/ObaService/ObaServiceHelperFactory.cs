﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OneBusAway.Model;
using OneBusAway.Utilities;

namespace OneBusAway.DataAccess.ObaService
{
    /// <summary>
    /// This class wraps HttpWebRequest and makes it easier to read / write data to a REST web service.
    /// </summary>
    public class ObaServiceHelperFactory
    {
        /// <summary>
        /// This is the URL of the regions web service.
        /// </summary>
        private const string REGIONS_SERVICE_URI = "http://regions.onebusaway.org/regions.xml";

        /// <summary>
        /// This is the URL of the service that we're talking to.
        /// </summary>
        private string serviceUrl;

        /// <summary>
        /// A task that this class will wait on until we have the regions
        /// </summary>
        private static Task<Region[]> regionsLookupTask;

        /// <summary>
        /// This is the users longitude.
        /// </summary>
        private double usersLongitude;

        /// <summary>
        /// This is the user latitude.
        /// </summary>
        private double usersLatitude;

        /// <summary>
        /// Static constructor creates the regions task.
        /// </summary>
        static ObaServiceHelperFactory()
        {
            regionsLookupTask = Task.Run(async () =>
                {
                    // Refresh once a week. Should be often enough.
                    XDocument doc = await ObaCache.GetCache(ObaMethod.regions, "ALL", 24 * 60 * 60 * 7);

                    if (doc == null)
                    {
                        var webRequest = WebRequest.CreateHttp(REGIONS_SERVICE_URI);

                        var response = await webRequest.GetResponseAsync();
                        var responseStream = response.GetResponseStream();

                        using (var streamReader = new StreamReader(responseStream))
                        {
                            string xml = await streamReader.ReadToEndAsync();
                            doc = XDocument.Parse(xml);
                        }
                    }

                    return (from regionElement in doc.Descendants("region")
                            let region = new Region(regionElement)
                            where region.IsActive && region.SupportsObaRealtimeApis
                            select region).ToArray();
                });
        }

        /// <summary>
        /// Creates the service helper.
        /// </summary>
        public ObaServiceHelperFactory(double usersLatitude, double usersLongitude)
        {
            this.usersLatitude = usersLatitude;
            this.usersLongitude = usersLongitude;
        }

        /// <summary>
        /// Factory method creates a service helper.
        /// </summary>
        public virtual async Task<IObaServiceHelper> CreateHelperAsync(ObaMethod obaMethod, HttpMethod httpMethod = HttpMethod.GET)
        {
            // Find the region that matches the users current location:
            var serviceUrl = (from region in await regionsLookupTask
                              where region.FallsInside(this.usersLatitude, this.usersLongitude)
                              select region.RegionUrl).FirstOrDefault();

            if (serviceUrl == null)
            {
                throw new UnknownRegionException();
            }

            return new ObaServiceHelper(serviceUrl, obaMethod, httpMethod);
        }

        /// <summary>
        /// Private implementation so that clients are forced to use the create method to talk to a OBA web service.
        /// </summary>
        private class ObaServiceHelper : IObaServiceHelper
        {
            /// <summary>
            /// This Uri builder is used to create the URI of the OBA REST service.
            /// </summary>
            private UriBuilder uriBuilder;

            /// <summary>
            /// Creates the web request.
            /// </summary>
            private HttpWebRequest request;

            /// <summary>
            /// The http method.
            /// </summary>
            private HttpMethod httpMethod;

            /// <summary>
            /// The oba method.
            /// </summary>
            private ObaMethod obaMethod;

            /// <summary>
            /// The service Url.
            /// </summary>
            private string serviceUrl;

            /// <summary>
            /// Maps name / value pairs to the query string.
            /// </summary>
            private Dictionary<string, string> queryStringMap;

            /// <summary>
            /// Creates the service helper.
            /// </summary>
            public ObaServiceHelper(string serviceUrl, ObaMethod obaMethod, HttpMethod httpMethod)
            {
                this.obaMethod = obaMethod;
                this.httpMethod = httpMethod;
                this.serviceUrl = serviceUrl;

                this.uriBuilder = new UriBuilder(serviceUrl);
                this.SetDefaultPath();

                this.queryStringMap = new Dictionary<string, string>();
                this.queryStringMap["key"] = UtilitiesConstants.API_KEY;
            }
            
            /// <summary>
            /// Adds a name / value pair to the query string.
            /// </summary>
            public void AddToQueryString(string name, string value)
            {
                this.queryStringMap[name] = value;
            }

            /// <summary>
            /// Sets the id for the rest query, if it exists.
            /// </summary>
            public void SetId(string id)
            {
                this.uriBuilder = new UriBuilder(serviceUrl);
                this.SetPath(id);
            }

            /// <summary>
            /// Sets the default path.
            /// </summary>
            private void SetDefaultPath()
            {
                this.SetPath(null);
            }

            /// <summary>
            /// Sets the path of the uri.
            /// </summary>
            /// <param name="id">The ID to set</param>
            private void SetPath(string id)
            {
                // If the URI we get back is missing a backslash, add it first:
                if (!String.IsNullOrEmpty(this.uriBuilder.Path) && !this.uriBuilder.Path.EndsWith("/"))
                {
                    this.uriBuilder.Path += "/";
                }

                this.uriBuilder.Path += "api/where/";

                string obaMethodString = obaMethod.ToString();
                obaMethodString = obaMethodString.Replace('_', '-');

                if (!string.IsNullOrEmpty(id))
                {
                    obaMethodString += "/";
                    obaMethodString += id;
                }

                obaMethodString += ".xml";
                this.uriBuilder.Path += obaMethodString;
            }

            /// <summary>
            /// Sends a payload to the service asynchronously.
            /// </summary>
            public async Task<XDocument> SendAndRecieveAsync(string payload)
            {                
                while (true)
                {
                    try
                    {
                        this.uriBuilder.Query = this.CreateQueryString();
                        this.request = WebRequest.CreateHttp(this.uriBuilder.Uri);
                        this.request.Method = this.httpMethod.ToString();

                        XDocument doc = await WebRequestQueue.SendAsync(request);

                        // Verify that OBA sent us a valid document and that it's status code is 200:                
                        int returnCode = doc.Root.GetFirstElementValue<int>("code");
                        if (returnCode != 200)
                        {
                            string text = doc.Root.GetFirstElementValue<string>("text");
                            throw new ObaException(returnCode, text);
                        }

                        return doc;
                    }
                    catch (ObaException e)
                    {
                        if (e.ErrorCode != 401)
                        {
                            throw;
                        }
                    }
                    catch (IOException)
                    {
                        // ignored....
                    }

                    // If we keep getting 401s (permission denied), then we just need to keep retrying.
                    await Task.Delay(20);
                }
            }

            /// <summary>
            /// Creates the query string out of the current queryStringMap object.
            /// </summary>
            private string CreateQueryString()
            {
                return string.Join("&", from keyValuePair in this.queryStringMap
                                        select string.Format(CultureInfo.CurrentCulture, "{0}={1}", keyValuePair.Key, keyValuePair.Value));

            }
        }
    }
}
