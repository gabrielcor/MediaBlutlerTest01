﻿using MediaButler.Common.ResourceAccess;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaButler.BaseProcess
{
   
    class GenerateThumbNailsStep:MediaButler.Common.workflow.StepHandler
    {
        private ButlerProcessRequest myRequest;
        private CloudMediaContext _MediaServicesContext;
        private IAsset myAssetOriginal;

        private IJob currentJob;
        /// <summary>
        /// Load encoding profile from local disk or Blob Storage (mediabultlerbin container)
        /// </summary>
        /// <param name="profileInfo"></param>
        /// <returns></returns>
        private string LoadEncodeProfile(string profileInfo)
        {
            string auxProfile = null;
            if (File.Exists(Path.GetFullPath(@".\encodingdefinitions\" + profileInfo)))
            {
                //the XML file is in the local file
                Trace.TraceInformation("[{0}] process Type {1} instance {2} Encoder Profile {3} from local disk", this.GetType().FullName, myRequest.ProcessTypeId, myRequest.ProcessInstanceId, profileInfo);
                auxProfile = File.ReadAllText(Path.GetFullPath(@".\encodingdefinitions\" + profileInfo));
            }
            else
            {
                //The profile is not in local file

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(myRequest.ProcessConfigConn);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(MediaButler.Common.Configuration.ButlerExternalInfoContainer);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(profileInfo);
                if (blockBlob.Exists())
                {
                    Trace.TraceInformation("[{0}] process Type {1} instance {2} Encoder Profile {3} from Blob Storage", this.GetType().FullName, myRequest.ProcessTypeId, myRequest.ProcessInstanceId, profileInfo);
         
                     using (var memoryStream = new MemoryStream())
                    {
                        blockBlob.DownloadToStream(memoryStream);
                        memoryStream.Position = 0;
                        StreamReader sr = new StreamReader(memoryStream);
                        auxProfile = sr.ReadToEnd();
                    }
                }
                else
                {
                    string txtMessage = string.Format("[{0}] process Type {1} instance {2} Error Encoder Profile {3} don't exist", this.GetType().FullName, myRequest.ProcessTypeId, myRequest.ProcessInstanceId, profileInfo);
                    throw (new Exception(txtMessage));
                }
                
            }
            return auxProfile;
        }
        private  void  GenerateThumbNails(IAsset assetToConvert)
        {
            //0 Helper
            IEncoderSupport myEncodigSupport = new EncoderSupport(_MediaServicesContext);
  
            // Declare a new job to contain the tasks
            currentJob = _MediaServicesContext.Jobs.Create("Generate ThumbNails for" +  myAssetOriginal.Name);

            // Get a media packager reference
            IMediaProcessor processor = myEncodigSupport.GetLatestMediaProcessorByName("Windows Azure Media Encoder");
            
            // Create a task with the thumbnail generation
            string xmlThumbProfile = "Thumbnails.xml";
            if (!string.IsNullOrEmpty(this.StepConfiguration)) // If there is a StepConfiguration 
            {
                xmlThumbProfile= this.StepConfiguration;
            }

            string configMp4Thumbnails = LoadEncodeProfile(xmlThumbProfile);
            ITask thumbtask = currentJob.Tasks.AddNew("Task profile " + xmlThumbProfile,
                processor,
                configMp4Thumbnails,
                TaskOptions.None);
            thumbtask.InputAssets.Add(assetToConvert);
            thumbtask.OutputAssets.AddNew(assetToConvert.Name + "_thumb", AssetCreationOptions.None);

            // Use the following event handler to check job progress. 
            // The StateChange method is the same as the one in the previous sample
            //currentJob.StateChanged += new EventHandler<JobStateChangedEventArgs>(StateChanged);
            currentJob.StateChanged += new EventHandler<JobStateChangedEventArgs>(myEncodigSupport.StateChanged);
            // Launch the job.
            currentJob.Submit();
            //9. Revisa el estado de ejecución del JOB 
            Task progressJobTask = currentJob.GetExecutionProgressTask(CancellationToken.None);

            //10. en vez de utilizar  progressJobTask.Wait(); que solo muestra cuando el JOB termina
            //se utiliza el siguiente codigo para mostrar avance en porcentaje, como en el portal
            myEncodigSupport.WaitJobFinish(currentJob.Id);
            
        }
        public override void HandleExecute(Common.workflow.ChainRequest request)
        {
            myRequest = (ButlerProcessRequest)request;

            _MediaServicesContext = new CloudMediaContext(myRequest.MediaAccountName, myRequest.MediaAccountKey);

            myAssetOriginal = (from m in _MediaServicesContext.Assets select m).Where(m => m.Id == myRequest.AssetId).FirstOrDefault();
            
            GenerateThumbNails(myAssetOriginal);
            
            // Update ThumbNailAsset
            myRequest.ThumbNailAsset = currentJob.OutputMediaAssets.FirstOrDefault();
        }
        public override void HandleCompensation(Common.workflow.ChainRequest request)
        {
            string txtTrace;

            if (currentJob!=null )
            {
                foreach (IAsset item in currentJob.OutputMediaAssets)
                {
                    txtTrace = string.Format("[{0}] process Type {1} instance {2} deleted Asset id={3}", this.GetType().FullName, myRequest.ProcessTypeId, myRequest.ProcessInstanceId, item.Id);
                    item.Delete();
                    Trace.TraceWarning(txtTrace);
                }
            }

            if (myAssetOriginal!=null)
            {
                txtTrace = string.Format("[{0}] process Type {1} instance {2} deleted Asset id={3}", this.GetType().FullName, myRequest.ProcessTypeId, myRequest.ProcessInstanceId, myAssetOriginal.Id);
                   
                myAssetOriginal.Delete();
                Trace.TraceWarning(txtTrace);
            }
        }
    }
}
