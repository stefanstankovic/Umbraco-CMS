﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientDependency.Core.CompositeFiles.Providers;
using ClientDependency.Core.Config;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Hosting;
using Umbraco.Core.Runtime;
using Umbraco.Web.JavaScript;

namespace Umbraco.Web.Runtime
{
    [ComposeAfter(typeof(WebInitialComponent))]
    public sealed class ClientDependencyComponent : IComponent
    {
        private readonly IHostingSettings _hostingSettings;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IRuntimeSettings _settings;

        public ClientDependencyComponent(
            IHostingSettings hostingSettings,
            IHostingEnvironment hostingEnvironment,
            IRuntimeSettings settings,
            IRuntimeMinifier runtimeMinifier)
        {
            _hostingSettings = hostingSettings;
            _hostingEnvironment = hostingEnvironment;
            _settings = settings;
        }

        public void Initialize()
        {
            // anything else?

            if (_hostingEnvironment.IsHosted)
            {
                ConfigureClientDependency();
            }
        }

        private void ConfigureClientDependency()
        {
            // Backwards compatibility - set the path and URL type for ClientDependency 1.5.1 [LK]
            XmlFileMapper.FileMapDefaultFolder = Core.Constants.SystemDirectories.TempData.EnsureEndsWith('/') + "ClientDependency";
            BaseCompositeFileProcessingProvider.UrlTypeDefault = CompositeUrlType.Base64QueryStrings;

            // Now we need to detect if we are running 'Umbraco.Core.LocalTempStorage' as EnvironmentTemp and in that case we want to change the CDF file
            // location to be there
            if (_hostingSettings.LocalTempStorageLocation == LocalTempStorage.EnvironmentTemp)
            {
                var cachePath = _hostingEnvironment.LocalTempPath;

                //set the file map and composite file default location to the %temp% location
                BaseCompositeFileProcessingProvider.CompositeFilePathDefaultFolder
                    = XmlFileMapper.FileMapDefaultFolder
                    = Path.Combine(cachePath, "ClientDependency");
            }

            if (_settings.MaxQueryStringLength.HasValue || _settings.MaxRequestLength.HasValue)
            {
                //set the max url length for CDF to be the smallest of the max query length, max request length
                ClientDependency.Core.CompositeFiles.CompositeDependencyHandler.MaxHandlerUrlLength = Math.Min(_settings.MaxQueryStringLength.GetValueOrDefault(), _settings.MaxRequestLength.GetValueOrDefault());
            }

            //Register a custom renderer - used to process property editor dependencies
            var renderer = new DependencyPathRenderer();
            renderer.Initialize("Umbraco.DependencyPathRenderer", new NameValueCollection
            {
                { "compositeFileHandlerPath", ClientDependencySettings.Instance.CompositeFileHandlerPath }
            });

            ClientDependencySettings.Instance.MvcRendererCollection.Add(renderer);
        }
        public void Terminate()
        { }
    }
}
