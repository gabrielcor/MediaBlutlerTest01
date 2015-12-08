﻿using MediaButler.Common;
using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaButler.BaseProcess
{
    class CreateStreamingLocatorStep: MediaButler.Common.workflow.StepHandler
    {
        private ButlerProcessRequest myRequest;
        private CloudMediaContext _MediaServiceContext;
      
        private ILocator CreateStreamingLocator(string  outputAssetid)
        {
            IAssetFile assetFile = null;
            ILocator locator = null;

       
            var outputAsset = _MediaServiceContext.Assets.Where(a => a.Id == outputAssetid).FirstOrDefault();
            
            var accessPolicy = _MediaServiceContext.AccessPolicies.Create(
                outputAsset.Name
                , TimeSpan.FromDays(Configuration.daysForWhichStreamingUrlIsActive)
                , AccessPermissions.Read);
            var assetFiles = outputAsset.AssetFiles.ToList();

            assetFile = assetFiles.Where(f => f.Name.ToLower().EndsWith(".ism")).FirstOrDefault();
            locator = _MediaServiceContext.Locators.CreateLocator(LocatorType.OnDemandOrigin, outputAsset, accessPolicy, DateTime.UtcNow.AddMinutes(-5));
            return locator;
        }
        public override void HandleExecute(Common.workflow.ChainRequest request)
        {
            myRequest = (ButlerProcessRequest)request;
            _MediaServiceContext = new CloudMediaContext(myRequest.MediaAccountName, myRequest.MediaAccountKey);
            
            var locator=CreateStreamingLocator(myRequest.AssetId);
            
        }

        public override void HandleCompensation(Common.workflow.ChainRequest request)
        {
            Trace.TraceWarning("{0} in process {1} processId {2} has not HandleCompensation", this.GetType().FullName, myRequest.ProcessTypeId, myRequest.ProcessInstanceId);
        }
    }
}
