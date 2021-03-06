﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. http://www.apache.org/licenses/LICENSE-2.0..
//
using FluentFTP;
using Renci.SshNet;
using Seal.Helpers;
using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Serialization;

namespace Seal.Model
{
    /// <summary>
    /// OutputFileServerDevice is an implementation of device that save the report result to a file server (FTP,SFTP,etc.).
    /// </summary>
    public class OutputFileServerDevice : OutputDevice
    {
        static string PasswordKey = "?d_*er)wien?,édl+25.()à,";

        public const string ProcessingScriptTemplate = @"@using Renci.SshNet
@using FluentFTP
@using System.IO
@using System.Security.Authentication
@{
    //Upload the file to the server
    Report report = Model;
    ReportOutput output = report.OutputToExecute;
    OutputFileServerDevice device = (OutputFileServerDevice)output.Device;

    var resultFileName = (output.ZipResult ? Path.GetFileNameWithoutExtension(report.ResultFileName) + "".zip"" : report.ResultFileName);
    device.HandleZipOptions(report);

    //Put file
    var remotePath = output.FileServerFolderWithSeparators + resultFileName;

    if (device.Protocol == FileServerProtocol.FTP)
    {
        //Refer to https://github.com/robinrodricks/FluentFTP
        using (var client = new FtpClient(device.HostName, device.UserName, device.ClearPassword)) {
            if (device.PortNumber == 0) {
                client.AutoConnect();
            }
            else {
                client.Port = device.PortNumber;
                //SSL Configuration can be defined here
                /*
                client.EncryptionMode = FtpEncryptionMode.Explicit;
                client.SslProtocols = SslProtocols.Tls12;
                client.ValidateCertificate += new FtpSslValidation(delegate(FtpClient control, FtpSslValidationEventArgs e) {
                    // add logic to test if certificate is valid here
                    e.Accept = true;
                });
                */
                client.Connect();
            }
            client.UploadFile(report.ResultFilePath, remotePath);
            client.Disconnect();
        }
    }
    else if (device.Protocol == FileServerProtocol.SFTP)
    {
        //Refer to https://github.com/sshnet/SSH.NET
        using (var sftp = new SftpClient(device.HostName, device.PortNumber, device.UserName, device.ClearPassword))
        {
            sftp.Connect();
            using (Stream fileStream = File.Create(report.ResultFilePath))
            {
                sftp.UploadFile(fileStream, remotePath);
            }
            sftp.Disconnect();
        }

    }
    else if (device.Protocol == FileServerProtocol.SCP)
    {
        //Refer to https://github.com/sshnet/SSH.NET
        using (var scp = new ScpClient(device.HostName, device.PortNumber, device.UserName, device.ClearPassword))
        {
            scp.Connect();
            using (Stream fileStream = File.Create(report.ResultFilePath))
            {
                scp.Upload(fileStream, remotePath);
            }
            scp.Disconnect();
        }
    }

    output.Information = report.Translate(""Report result generated in '{0}'"", remotePath);
    report.LogMessage(""Report result generated in '{0}'"", remotePath);
}
";


        /// <summary>
        /// Default device identifier
        /// </summary>
        public static string DefaultGUID = "c428a6ba-061b-4a47-b9bc-f3f02442ab4b";

        /// <summary>
        /// Last modification date time
        /// </summary>
        [XmlIgnore]
        public DateTime LastModification;

        /// <summary>
        /// Create a basic OutputFolderDevice
        /// </summary>
        static public OutputFileServerDevice Create()
        {

            var result = new OutputFileServerDevice() { GUID = Guid.NewGuid().ToString() };
            result.Name = "File Server Device";
            return result;
        }

        /// <summary>
        /// Full name
        /// </summary>
        [XmlIgnore]
        public override string FullName
        {
            get { return string.Format("{0} (File Server)", Name); }
        }

        /// <summary>
        /// Protocol to connect to the server
        /// </summary>
        public FileServerProtocol Protocol { get; set; } = FileServerProtocol.FTP;

        /// <summary>
        /// File Server host name
        /// </summary>
        public string HostName { get; set; } = "127.0.0.1";

        /// <summary>
        /// Port number used to connect to the server (e.g. 21 for FTP, 22 for SFTP, 990 for FTPS, etc.)
        /// </summary>
        public int PortNumber { get; set; } = 0;


        /// <summary>
        /// List of directories allowed on the file server. One per line or separated by semi-column.
        /// </summary>
        public string Directories { get; set; } = "/";

        /// <summary>
        /// Array of allowed directories.
        /// </summary>
        public string[] DirectoriesArray
        {
            get
            {
                return Directories.Trim().Replace("\r\n", "\r").Split('\r');
            }
        }

        /// <summary>
        /// The user name used to connect to the File Server
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// The password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The clear password used to connect to the File Server
        /// </summary>
        [XmlIgnore]
        public string ClearPassword
        {
            get
            {
                try
                {
                    return CryptoHelper.DecryptTripleDES(Password, PasswordKey);
                }
                catch (Exception ex)
                {
                    Error = "Error during password decryption:" + ex.Message;
                    TypeDescriptor.Refresh(this);
                    return Password;
                }
            }
            set
            {
                try
                {
                    Password = CryptoHelper.EncryptTripleDES(value, PasswordKey);
                }
                catch (Exception ex)
                {
                    Error = "Error during password encryption:" + ex.Message;
                    Password = value;
                    TypeDescriptor.Refresh(this);
                }
            }
        }

        /// <summary>
        /// Script executed when the output is processed. The script can be modified to change the client settings (e.g. configuring FTPS).
        /// </summary>
        public string ProcessingScript { get; set; } = "";

        /// <summary>
        /// Last information message
        /// </summary>
        [XmlIgnore]
        public string Information { get; set; }

        /// <summary>
        /// Last error message
        /// </summary>
        [XmlIgnore]
        public string Error { get; set; }

        /// <summary>
        /// Check that the report result has been saved and set information
        /// </summary>
        public override void Process(Report report)
        {
            var script = string.IsNullOrEmpty(ProcessingScript) ? ProcessingScriptTemplate : ProcessingScript;
            RazorHelper.CompileExecute(script, report);
        }

        /// <summary>
        /// Load an OutputFileServerDevice from a file
        /// </summary>
        static public OutputDevice LoadFromFile(string path, bool ignoreException)
        {
            OutputFileServerDevice result = null;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(OutputFileServerDevice));
                using (XmlReader xr = XmlReader.Create(path))
                {
                    result = (OutputFileServerDevice)serializer.Deserialize(xr);
                }
                result.Name = Path.GetFileNameWithoutExtension(path);
                result.FilePath = path;
                result.LastModification = File.GetLastWriteTime(path);
            }
            catch (Exception ex)
            {
                if (!ignoreException) throw new Exception(string.Format("Unable to read the file '{0}'.\r\n{1}", path, ex.Message));
            }
            return result;
        }

        /// <summary>
        /// Save to current file
        /// </summary>
        public override void SaveToFile()
        {
            SaveToFile(FilePath);
        }

        /// <summary>
        /// Save to a file
        /// </summary>
        public override void SaveToFile(string path)
        {
            //Check last modification
            if (LastModification != DateTime.MinValue && File.Exists(path))
            {
                DateTime lastDateTime = File.GetLastWriteTime(path);
                if (LastModification != lastDateTime)
                {
                    throw new Exception("Unable to save the Output Device file. The file has been modified by another user.");
                }
            }

            Name = Path.GetFileNameWithoutExtension(path);
            XmlSerializer serializer = new XmlSerializer(typeof(OutputFileServerDevice));
            XmlWriterSettings ws = new XmlWriterSettings();
            ws.NewLineHandling = NewLineHandling.Entitize;
            using (XmlWriter xw = XmlWriter.Create(path, ws))
            {
                serializer.Serialize(xw, this);
            }
            FilePath = path;
            LastModification = File.GetLastWriteTime(path);
        }

        /// <summary>
        /// Validate the device
        /// </summary>
        public override void Validate()
        {
            if (string.IsNullOrEmpty(HostName)) throw new Exception("The File Server cannot be empty.");
        }


        /// <summary>
        /// Helper to test the connection
        /// </summary>
        public void TestConnection()
        {
            try
            {
                Error = "";
                Information = "";

                if (Protocol == FileServerProtocol.FTP)
                {
                    FtpClient client = new FtpClient(HostName, UserName, ClearPassword);

                    if (PortNumber == 0)
                    {
                        client.AutoConnect();
                    }
                    else
                    {
                        client.Port = PortNumber;
                        client.Connect();
                    }
                    client.Disconnect();
                }
                else if (Protocol == FileServerProtocol.SFTP)
                {
                    using (var sftp = new SftpClient(HostName, UserName, ClearPassword))
                    {
                        sftp.Connect();
                        sftp.Disconnect();

                    }
                }
                else if (Protocol == FileServerProtocol.SCP)
                {
                    using (var scp = new ScpClient(HostName, UserName, ClearPassword))
                    {
                        scp.Connect();
                        scp.Disconnect();
                    }
                }
                Information = string.Format("The connection to '{0}:{1}' is successfull", HostName, PortNumber);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                if (ex.InnerException != null) Error += " " + ex.InnerException.Message.Trim();
                Information = "Error got testing the connection.";
            }
        }
        /// <summary>
        /// Editor Helper: Test the connection with the current configuration
        /// </summary>
        public string HelperTestConnection
        {
            get { return "<Click to test the connection>"; }
        }


        #region FtpHelpers
        /// <summary>
        /// Delegate to create a FtpWebRequest
        /// </summary>
        public delegate FtpWebRequest CustomFtpGetRequest(string destination, string method);

        /// <summary>
        /// Put directories and file to an FTP Server
        /// </summary>
        public void FtpPutDirectory(string source, string destination, bool recursive, ReportExecutionLog log, CustomFtpGetRequest ftpGetRequest, string searchPattern = "*")
        {
            if (ftpGetRequest == null) ftpGetRequest = FtpGetRequest;

            if (!FtpDirectoryExists(destination, ftpGetRequest)) { 
                if (log != null) log.LogMessage("Creating remote directory: " + destination);
                var request = ftpGetRequest(destination, WebRequestMethods.Ftp.MakeDirectory);
                var response = request.GetResponse();
                response.Close();
            }

            foreach (string file in Directory.GetFiles(source, searchPattern))
            {
                try
                {
                    var destinationFile = Path.Combine(destination, Path.GetFileName(file)).Replace("\\", "/");
                    if (log != null) log.LogMessage("Copy '{0}' to '{1}'", file, destinationFile);
                    FtpPutFile(file, destinationFile, ftpGetRequest);
                }
                catch (Exception ex)
                {
                    if (log != null) log.LogMessage(ex.Message);
                }
            }

            if (recursive)
            {
                foreach (string directory in Directory.GetDirectories(source))
                {
                    FtpPutDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)).Replace("\\", "/"), recursive, log, ftpGetRequest, searchPattern);
                }
            }
        }

        /// <summary>
        /// Put a file on a FTP server
        /// </summary>
        public void FtpPutFile(string source, string destination, CustomFtpGetRequest ftpGetRequest)
        {
            if (ftpGetRequest == null) ftpGetRequest = FtpGetRequest;
            FtpWebRequest request = ftpGetRequest(destination, WebRequestMethods.Ftp.UploadFile);
            byte[] fileContents = File.ReadAllBytes(source);
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(fileContents, 0, fileContents.Length);
            }
            var response = request.GetResponse();
            response.Close();
        }

        /// <summary>
        /// Read a file from a FTP server
        /// </summary>
        public string FtpReadFile(string source, CustomFtpGetRequest ftpGetRequest)
        {
            if (ftpGetRequest == null) ftpGetRequest = FtpGetRequest;
            FtpWebRequest request = ftpGetRequest(source, WebRequestMethods.Ftp.DownloadFile);
            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

            Stream responseStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(responseStream);
            var result = reader.ReadToEnd();
            reader.Close();
            response.Close();

            return result;
        }

        /// <summary>
        /// True if the directory exists on the server
        /// </summary>
        public bool FtpDirectoryExists(string destination, CustomFtpGetRequest ftpGetRequest)
        {
            if (ftpGetRequest == null) ftpGetRequest = FtpGetRequest;
            FtpWebRequest request = ftpGetRequest(destination, WebRequestMethods.Ftp.ListDirectory);
            try
            {
                var response = request.GetResponse();
                response.Close();
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// True if the file exists on the server
        /// </summary>
        public bool FtpFileExists(string destination, CustomFtpGetRequest ftpGetRequest)
        {
            if (ftpGetRequest == null) ftpGetRequest = FtpGetRequest;
            FtpWebRequest request = ftpGetRequest(destination, WebRequestMethods.Ftp.GetFileSize);
            try
            {
                var response = request.GetResponse();
                response.Close();
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get the FtpWebRequest (default function)
        /// </summary>
        public FtpWebRequest FtpGetRequest(string destination, string method)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(string.Format("ftp://{0}:{1}{2}", HostName, PortNumber, destination));
            request.KeepAlive = false;
            request.Method = method;
            request.Credentials = new NetworkCredential(UserName, ClearPassword);
            return request;
        }
    }
    #endregion
}

