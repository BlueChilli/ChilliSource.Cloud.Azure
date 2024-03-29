﻿using ImageResizer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageResizer.Configuration;
using System.Web.Hosting;
using System.Web;
using System.IO;
using System.Linq.Expressions;
using ImageResizer.ExtensionMethods;
using System.Security;
using ImageResizer.Configuration.Issues;

namespace ChilliSource.Cloud.Azure
{
    public class AzureReader : IPlugin
    {
        private bool _installed = false;
        private string _connectionString;
        private string _endpoint;
        private bool _cacheUnmodified;

        private string _prefix;
        private bool _asVpp;
        private AzureVirtualPathProvider _pathProvider;

        public AzureReader(System.Collections.Specialized.NameValueCollection args)
        {
            this._connectionString = args["connectionstring"] ?? "";
            this._endpoint = args["endpoint"] ?? "";
            this._prefix = args["prefix"] ?? "";
            this._asVpp = NameValueCollectionExtensions.Get(args, "vpp", true);
            this._cacheUnmodified = Convert.ToBoolean(args["cacheunmodified"] ?? "true");

            if (string.IsNullOrEmpty(this._endpoint))
            {
                throw new System.InvalidOperationException("No endpoint found.");
            }

            if (string.IsNullOrEmpty(this._connectionString))
            {
                throw new System.InvalidOperationException("This plugin needs a connection string for the Azure blob storage.");
            }

            if (!this._endpoint.EndsWith("/"))
            {
                this._endpoint += "/";
            }
            if (string.IsNullOrEmpty(this._prefix))
            {
                this._prefix = "~/storage/";
            }
        }

        public IPlugin Install(Config c)
        {
            if (_installed)
            {
                throw new System.InvalidOperationException("Plugin already installed.");
            }

            this._pathProvider = new AzureVirtualPathProvider(this._connectionString, this._prefix);
            c.Pipeline.PostRewrite += this.Pipeline_PostRewrite;

            if (this._asVpp)
            {
                try
                {
                    HostingEnvironment.RegisterVirtualPathProvider((VirtualPathProvider)this._pathProvider);
                }
                catch (SecurityException ex)
                {
                    this._asVpp = false;
                    c.configurationSectionIssues.AcceptIssue((IIssue)new Issue("AzureReader", "AzureReader could not be installed as a VirtualPathProvider due to missing AspNetHostingPermission.", "It was installed as an IVirtualImageProvider instead, which means that only image URLs will be accessible, and only if they contain a querystring.\nSet vpp=false to tell AzureReader to register as an IVirtualImageProvider instead. <add name='AzureReader' vpp=false />", IssueSeverity.Error));
                }
            }
            if (!this._asVpp)
                c.Plugins.VirtualProviderPlugins.Add(this._pathProvider);

            c.Plugins.add_plugin(this);

            _installed = true;
            return this;
        }

        private void Pipeline_PostRewrite(IHttpModule sender, HttpContext context, IUrlEventArgs e)
        {
            if (e.QueryString.Count == 0 && this._pathProvider.IsAzurePath(e.VirtualPath))
            {
                //cache - always|no|default - Always forces the image to be cached even if it wasn't modified by the resizing module. Doesn't disable caching if it was modified.
                e.QueryString["cache"] = _cacheUnmodified ? "always" : "no";

                //process - always|no|default - Always forces the image to be re-encoded even if it wasn't modified. Does not prevent the image from being modified.

                //We need this when there's no query string to be able to call the virtual path provider
                e.QueryString["process"] = "always";
            }
        }

        public bool Uninstall(Config c)
        {
            if (this._asVpp)
                return false;
            c.Plugins.VirtualProviderPlugins.Remove(this._pathProvider);
            return true;
        }
    }
}
