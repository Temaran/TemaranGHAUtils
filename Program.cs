﻿namespace S3Uploader
{
	using System;
	using Amazon;
	using Amazon.S3;
	using Amazon.S3.Transfer;
	using CommandLine;
	using System.IO;
	using System.IO.Compression;

	class Program
	{
		public class Options
		{
			[Option('k', "key", Required = true, HelpText = "Your S3 key.")]
			public string AWSKey { get; set; }

			[Option('s', "secretkey", Required = true, HelpText = "Your S3 secret key.")]
			public string AWSSecretKey { get; set; }

			[Option('r', "region", Required = false, HelpText = "Your S3 region.")]
			public string AWSRegion { get; set; }

			[Option('p', "path", Required = true, HelpText = "Directory to zip and upload, or file to upload.")]
			public string Path { get; set; }

			[Option('b', "bucket", Required = true, HelpText = "The bucket to upload to.")]
			public string Bucket { get; set; }

			[Option('n', "name", Required = false, HelpText = "Optional new name to use in S3.")]
			public string S3Name { get; set; }

			[Option('d', "subdir", Required = false, HelpText = "Optional subdir to upload to in S3.")]
			public string S3Subdir { get; set; }
		}

		static int Main(string[] args)
		{
			int returnVal = 3;
			Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
			{
				Console.ForegroundColor = ConsoleColor.Red;
				if (options == null)
				{
					Console.WriteLine("UploadToS3 Error: Could not parse arguments.");
					return;
				}

				if (options.AWSKey.Length <= 0 || options.AWSSecretKey.Length <= 0)
				{
					Console.WriteLine("UploadToS3 Error: AWSKey or AWSSecretKey are not valid. You must provide these. See here how to generate them: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_access-keys.html");
					return;
				}

				if (!Directory.Exists(options.Path) && !File.Exists(options.Path))
				{
					Console.WriteLine("UploadToS3 Error: Input path must be a valid file or directory. This is neither: {0}", options.Path);
					return;
				}

				if (string.IsNullOrEmpty(options.Bucket))
				{
					Console.WriteLine("UploadToS3 Error: You must specify a bucket to use.");
					return;
				}
				options.Bucket = options.Bucket.ToLower();

				Console.ForegroundColor = ConsoleColor.White;

				var endPoint = string.IsNullOrEmpty(options.AWSRegion) ? RegionEndpoint.EUNorth1 : RegionEndpoint.GetBySystemName(options.AWSRegion);
				var client = new AmazonS3Client(options.AWSKey.ToString(), options.AWSSecretKey.ToString(), endPoint);
				var utility = new TransferUtility(client);
				FileAttributes pathAttributes = File.GetAttributes(options.Path);
				if (pathAttributes.HasFlag(FileAttributes.Directory))
				{
					returnVal = UploadDirectoryToS3(options, utility);
				}
				else
				{
					returnVal = UploadFileToS3(options, utility);
				}
			});

			return returnVal;
		}

		private static int UploadDirectoryToS3(Options options, TransferUtility utility)
		{
			Console.WriteLine("UploadToS3: Attempting to upload directory " + options.Path);
			try
			{
				var path = new DirectoryInfo(options.Path);
				if (path == null || path.Parent == null)
				{
					return 2;
				}

				var request = new TransferUtilityUploadRequest
				{
					BucketName = string.IsNullOrEmpty(options.S3Subdir) ? options.Bucket : (options.Bucket + @"/" + options.S3Subdir),
					Key = string.IsNullOrEmpty(options.S3Name) ? path.Name : options.S3Name
				};

				var archivePath = Path.Combine(path.Parent.FullName, "TempS3Archive.zip");
				Console.WriteLine("UploadToS3: Creating temporary archive to upload in the parent dir: " + archivePath);
				File.Delete(archivePath);
				ZipFile.CreateFromDirectory(options.Path, archivePath);

				Console.WriteLine("UploadToS3: Uploading archive...");
				request.FilePath = archivePath;
				request.UploadProgressEvent += Request_UploadProgressEvent;
				utility.Upload(request);

				Console.WriteLine("UploadToS3: Deleting temporary archive.");
				File.Delete(archivePath);
				return 0;
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("UploadToS3 Error: Could not upload directory: " + e.ToString());
				return 1;
			}
		}

		private static int UploadFileToS3(Options options, TransferUtility utility)
		{
			Console.WriteLine("UploadToS3: Attempting to upload file " + options.Path);
			try
			{
				var request = new TransferUtilityUploadRequest
				{
					BucketName = string.IsNullOrEmpty(options.S3Subdir) ? options.Bucket : (options.Bucket + @"/" + options.S3Subdir),
					Key = string.IsNullOrEmpty(options.S3Name) ? Path.GetFileNameWithoutExtension(options.Path) : options.S3Name,
					FilePath = options.Path
				};
				request.UploadProgressEvent += Request_UploadProgressEvent;

				Console.WriteLine("UploadToS3: Uploading file...");
				utility.Upload(request);
				return 0;
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("UploadToS3 Error: Could not upload file: " + e.ToString());
				return 1;
			}
		}

		private static void Request_UploadProgressEvent(object sender, UploadProgressArgs e)
		{
			Console.WriteLine("UploadToS3: Upload progress: " + e.PercentDone + "%");
		}
	}
}
