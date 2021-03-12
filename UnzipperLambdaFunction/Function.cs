using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UnzipperLambda
{
    public class Function
    {
        private static string srcBucketName = "";                      //triggering bucket

        private static string tmpExtractPath = "";                     //tmp directory for zip extraction
        private static string tmpFileName = "";                        //temporary name for extracted zip

        private static string dstBucketName = "YOUR BUCKET NAME";      //destination bucket
        private static string extractedZipFilePath = "";               //dst path(bucket+directory) in which the zip file will be unzipped

        private static string zipFileName = "";                        //detected zip file name
        private static string zipFileNameWithoutExtension = "";        //detected zip file name without its extension

        private static bool ifZipFileExtractedSuccessfully = true;     //flag indicating the success or failure of zip extraction
     
        IAmazonS3 S3Client { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }

        /// <summary>
        /// This method is called for every Lambda invocation.
        /// This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// you have to add a s3 trigger with .zip extension to this lambda 
        /// to receive the zip creation events from your s3 bucket
        /// </summary>
        /// <param name="evnt">received event from s3</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            //step1: lambda configurations
            Initialization(evnt, context);
            //step2: extract zip file into temp directory
            await ExtractZipFile(evnt, context);
            //step 3: copy extracted files from temp directory to destination bucket
            await CopyExtractedFiles(evnt, context);
            //step 4: clean up temp directories and source bucket files
            CleanUp(evnt, context);
       
            return true;
        }


        /// <summary>
        /// Clean up phase: delete  temporary created files and original zip file(if needed).
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        private async void CleanUp(S3Event evnt, ILambdaContext context)
        {
            try
            {
                //uncomment following lines if you want to delete the source zip file after extraction process.
                //by default, the function does not delete these files to have a backup from zips.
                //    if (ifZipFileExtractedSuccessfully == true)
                //      await this.S3Client.DeleteBucketAsync(zipFileBucketName, new CancellationToken());
                if (System.IO.File.Exists(tmpFileName))
                    System.IO.File.Delete(tmpFileName);
            }
            catch (Exception e)
            {
                context.Logger.LogLine("Error in cleaning up phase:" + e.Message);
            }
        }

        /// <summary>
        ///Get files one by one from /tmp/{zipFileNameWithoutExtension} directory
        ///and copy them to bucket bucketName/{zipFileNameWithoutExtension}
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task CopyExtractedFiles(S3Event evnt, ILambdaContext context)
        {
            foreach (FileInfo sourceFile in new DirectoryInfo(tmpExtractPath).GetFiles())
            {
                FileInfo filename = sourceFile;
                string contents = File.ReadAllText(filename.FullName);
                try
                {
                    PutObjectRequest putObjectRequest = new PutObjectRequest();
                    putObjectRequest.ContentBody = contents;
                    putObjectRequest.BucketName = extractedZipFilePath;
                    putObjectRequest.Key = filename.Name;
                    PutObjectResponse putObjectResponse = await this.S3Client.PutObjectAsync(putObjectRequest);
                    context.Logger.LogLine("File " + sourceFile.Name + 
                        " copied successfully to bucket " + extractedZipFilePath);
                }
                catch (Exception e)
                {
                    context.Logger.LogLine("Error in copying extracted files:" + e.Message);
                    ifZipFileExtractedSuccessfully = false;
                }
            }

        }

        /// <summary>
        ///Extract detected zip file into the lambda /tmp/{zipFileNameWithoutExtension} directory 
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task ExtractZipFile(S3Event evnt, ILambdaContext context)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest();
                request.BucketName = srcBucketName;
                request.Key = zipFileName;
                GetObjectResponse response1 = await this.S3Client.GetObjectAsync(request);
                await response1.WriteResponseStreamToFileAsync(tmpFileName, true, new CancellationToken());
                ZipFile.ExtractToDirectory(tmpFileName, tmpExtractPath);
                context.Logger.LogLine("Zip File extracted successfully to:" + tmpExtractPath + ", Temp file name: " + tmpFileName);
            }
            catch (Exception e)
            {
                context.Logger.LogLine("Error in Extracting zip file: " + e.Message);
            }
        }

        /// <summary>
        /// Needed initialization before running the main logic of the function
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        private void Initialization(S3Event evnt, ILambdaContext context)
        {
            try
            {
                var s3Event = evnt.Records?[0].S3;
                if (s3Event == null)
                    context.Logger.LogLine("No events detected from S3. Lambda function returns!");
                
                zipFileName = s3Event.Object.Key;

                context.Logger.LogLine("Zip file " + zipFileName + " detected!");

                zipFileNameWithoutExtension = zipFileName.Substring(0, zipFileName.Length - 4);
                
                srcBucketName = s3Event.Bucket.Name;
                
                extractedZipFilePath = dstBucketName + "/" + zipFileNameWithoutExtension;

                tmpExtractPath = "/tmp/" + zipFileNameWithoutExtension;
                tmpFileName = System.IO.Path.GetTempFileName();
                context.Logger.LogLine("Temp file name: " + tmpFileName);
            }
            catch (Exception e)
            {
                context.Logger.LogLine("Error in Initialization: " + e.Message);
            }
        }
    }
}
