# UnzipperLambdaFunction
C#/.NET Core lambda function for extracting zip files received in S3 buckets

Using this lambda function, you can detect the zip file events in S3 buckets and then extract the zips to another S3 bucket.

You need to define the destination bucket name in the code(the bucket you want to extract zips to) and then once you deployed your lambda into AWS, you need to add a S3 trigger(on source bucket) with 'oncreated' event on '.zip' extensions.

The execution plan is like this:
1. Lambda detects a zip in your source bucket
2. detected zip will be extracted to a temp directory in aws(/tmp/your extracted zip files)
3. extracted zip file will be copied from /tmp directory into the destination directory in the destination bucket.
4. lambda will clean up the /tmp directory(and input zip file, if selected in code) upon successfull execution.

