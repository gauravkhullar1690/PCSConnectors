///////////////////////////////////////////////////////////////// 
//
// Copyright (c) 2015 Paramount Computer System
// All Rights Reserved. 
//
// Permission to use, copy, modify, and distribute this 
// software is prohibited.
//
// THE AUTHOR(S)MAKE NO REPRESENTATIONS OR
// WARRANTIES ABOUT THE SUITABILITY OF THE SOFTWARE, EITHER 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
// PARTICULAR PURPOSE, OR NON-INFRINGEMENT. THE AUTHORS 
// AND PUBLISHER SHALL NOT BE LIABLE FOR ANY DAMAGES SUFFERED 
// BY LICENSEE AS A RESULT OF USING, MODIFYING OR DISTRIBUTING
// THIS SOFTWARE OR ITS DERIVATIVES.
// Author: Gaurav Khullar
// Date: 14 April 2015
// Version 1.0
///////////////////////////////////////////////////////////////// 

using System;
using System.Collections.Generic;
using System.Text;
using Courion.dotNetRDK;
using Courion.Types;
using System.Configuration;
using System.IO;
using System.Net;
using System.Data;
using System.Security.Cryptography;
using System.Xml;
using Sybase.Data.AseClient;

namespace ADIBAMSConnector
{
    public class ADIBAMSCnctr : RDKCore
    {
        // Target Validation Parameters
        private string m_sHost;
        private string m_sPort;
        private string m_sDatabaseName;
        private string m_sDBUserName;
        private string m_sDBPassword;


        // connection objects
        private AseConnection connection;
        private AseCommand command;

        // Operational Parameters
        private string m_sGroups;
        private string m_sRole;

        // Supporting variables
        private bool bErr = false;
        private string sErrMsg;
        private string sResult;
        // Charater used seprate multiple values
        private const char multiValueSeprator = ',';

        // Define the Log object and required variables
        private COURPROFILERLib.CourProfileLog2Class _log_obj = null;
        private string uid;
        private string _log_file;
        private bool _log_level = true;
        private int iConnectionTimeout = 15;
        private int iRetryCount = 0;

        private string m_sRequestXMLData;

        private void SetLogFile(string file_name)
        {
            this._log_file = file_name;
        }

        private void SetLogLevel(bool log_lvl)
        {
            this._log_level = log_lvl;
        }

        private void Log(string category, string msg)
        {
            if (this._log_file != null && this._log_file != "")
            {
                if (this._log_obj == null)
                {
                    this._log_obj = new COURPROFILERLib.CourProfileLog2Class();
                }
                if (this._log_level == true)
                {
                    msg = this.uid + " .Net: " + category + " - " + msg;
                    this._log_obj.Log(0, ref this._log_file, ref msg, 0);
                }
                else
                {
                    if (category == "INFO")
                    {
                        msg = this.uid + " .NET: " + category + " - " + msg;
                        this._log_obj.Log(0, ref this._log_file, ref msg, 0);
                    }
                }
            }
            else
            {
                throw new Exception("Log file is not configured.");
            }
        }

        private void Log(string msg)
        {
            //if (this._log_level != false && this._log_file != null && this._log_file != "")
            if (this._log_file != null && this._log_file != "")
            {
                if (this._log_obj == null)
                {
                    this._log_obj = new COURPROFILERLib.CourProfileLog2Class();
                }
                if (this._log_level == true)
                {
                    msg = this.uid + " .NET: " + msg;
                    this._log_obj.Log(0, ref this._log_file, ref msg, 0);
                }
            }
        }

        private void SetUID(string uid)
        {
            this.uid = uid;
        }

        private void LogWarning(string msg)
        {
            if (this._log_obj == null && this._log_file != null && this._log_file != "")
            {
                this._log_obj = new COURPROFILERLib.CourProfileLog2Class();
            }
            msg = this.uid + ".NET WARNING: " + msg;
            //Console.WriteLine(msg);
            this._log_obj.Log(0, ref this._log_file, ref msg, 0);
        }

        private void LogError(string msg)
        {
            if (this._log_obj == null && this._log_file != null && this._log_file != "")
            {
                this._log_obj = new COURPROFILERLib.CourProfileLog2Class();
            }
            msg = this.uid + ".NET Error: " + msg;
            //Console.WriteLine(msg);
            this._log_obj.Log(0, ref this._log_file, ref msg, 0);
        }

        private string GenerateRandomString(int maxSize)
        {
            char[] charSet = new char[62];
            charSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890".ToCharArray();

            byte[] data = new byte[1];

            RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider();
            crypto.GetNonZeroBytes(data);

            data = new byte[maxSize];
            crypto.GetNonZeroBytes(data);

            StringBuilder result = new StringBuilder(maxSize);

            foreach (byte b in data)
                result.Append(charSet[b % (charSet.Length)]);

            return result.ToString();
        }

        public ADIBAMSCnctr()
            : base("CourionAMSCnctr")
        {
            SetUID(GenerateRandomString(8)); // Generate a 8 character Random string
            string exeConfigPath = this.GetType().Assembly.Location;
            Configuration config = ConfigurationManager.OpenExeConfiguration(exeConfigPath);
            string attributesFile = string.Empty;
            if (config != null)
            {
                // Reading config file.
                string logFName = config.AppSettings.Settings["LogFileName"].Value;
                // Set the Log filename for the project
                this.SetLogFile(logFName);
                // Read the debug flag to enable/disable logging 
                string debugFlag = config.AppSettings.Settings["DebugLevel"].Value;

                // connection timeout
                string connectionTimeout = config.AppSettings.Settings["ConnectionTimeout"].Value;
                string retryCount = config.AppSettings.Settings["RetryCount"].Value;
                try
                {
                    iConnectionTimeout = Int16.Parse(connectionTimeout);
                    iRetryCount = Int16.Parse(retryCount);
                }
                catch (Exception)
                {

                }

                if (debugFlag == "0")
                {
                    // Logging is disabled.
                    SetLogLevel(false);
                }
                else
                {
                    //Logging is enabled.
                    SetLogLevel(true);
                }
                attributesFile = !string.IsNullOrEmpty(config.AppSettings.Settings["AttributeXML"].Value) ? config.AppSettings.Settings["AttributeXML"].Value : "ADIBAMSAttributes.xml";
            }

            base.PullAttributeXMLFile(System.IO.Directory.GetCurrentDirectory() + "\\" + attributesFile);


        }

        private void SetExceptionMessage(Exception ex)
        {
            if (ex.InnerException != null)
            {
                this.sErrMsg = ex.InnerException.Message;
            }
            else
            {
                this.sErrMsg = ex.Message;
            }
            Log(this.sErrMsg);
            this.bErr = true;
        }

        // This method is used to initialize the parameters from the request value
        private void setupConfig(RequestObject req)
        {
            try
            {
                Log("Inside setupConfig");

                // Fetch the target parameters configured

                this.m_sHost = req.GetParameter("Host");
                this.m_sPort = req.GetParameter("Port");
                this.m_sDatabaseName = req.GetParameter("DatabaseName");
                this.m_sDBUserName = req.GetParameter("DBUsername");
                this.m_sDBPassword = req.GetParameter("DBPassword");

                Log("Outside of setupConfig");
            }
            catch (Exception ex)
            {
                Log("Exception: " + ex.Message);
                throw new Exception(ex.Message);
            }
        } // setupConfig

        // This method opens the connection with Oracle database
        private void makeConnection(String host, String port, String serviceName, String username, String password, String methodName)
        {
            Log("DEBUG", "Trying to establish connection to the AMS database for method " + methodName);
            Log("DEBUG", "retryCount = " + iRetryCount + " connect timout= " + iConnectionTimeout);
            //Log("DEBUG", "Parameters host : " + host + " port : "+port + " serviceName : "+serviceName + " username : "+username +" password : "+password);  
            this.connection = new AseConnection();
            try
            {
                String connString = "Data Source='"+this.m_sHost+"';Port='"+this.m_sPort+"';UID='"+m_sDBUserName+"';PWD='"+m_sDBPassword+"';Database='"+m_sDatabaseName+"';";

                this.connection.ConnectionString = connString;

                this.connection.Open();
                Log("INFO", "Successfully connected to the AMS database for method " + methodName);
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to AMS database for method " + methodName + " :: " + e.Message);
                throw e;
            }

        }

        // This method closes the connection with Oracle database
        private void closeConnection(String methodName)
        {
            Log("DEBUG", "Closing database connection for method " + methodName);
            try
            {
                this.connection.Close();
                Log("INFO", "Connection closed successfully for method " + methodName);
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to AMS database :: " + e.Message);
                throw e;
            }
        }

        // This method is creates a Oracle Command
        private void executeCommand(String query, Dictionary<String, Object> parameters, String methodName)
        {
            try
            {
                this.command = new AseCommand(query, this.connection);
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Log("DEBUG", "executeCommand called failed " + query + " for method :: " + e.Message);
                Log("ERROR", "executeCommand called failed for method :: " + e.Message);
                throw e;
            }
        }

        // Execute Reader
        private StrList executeReader(String query, List<String> columns, String methodName)
        {
            StrList list = new StrList();
            try
            {
                AseCommand command = new AseCommand(query, this.connection);
                AseDataReader reader = null;
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    foreach (String col in columns)
                    {
                        list.Add(reader[col].ToString());
                    }
                }
                reader.Close();
            }
            catch (Exception e)
            {
                Log("DEBUG", "executeCommand called failed " + query + " for method :: " + e.Message);
                Log("ERROR", "executeCommand called failed for method :: " + e.Message);
                throw e;
            }
            return list;
        }        
        
        // initialize the attributes
        private void initializeAttributes(RequestObject reqObj, String methodName)
        {
            Log("DEBUG", "Initializing the attributes for method " + methodName);
            this.m_sGroups = reqObj.GetParameter("Groups");
            Log("DEBUG", "Attributes Initialized successfully for method " + methodName);
        }

        // check if the User is present in the AMS  
        private Boolean checkUserPresent(RequestObject reqObj)
        {
            String userId = reqObj.m_accountName;
            Log("INFO", "Checking if the User "+ userId + " exist in AMS application");
            AseCommand cmd = new AseCommand("SELECT count(*) AS UserExist FROM EACS_User where UserId='"+userId+"'", this.connection);
            AseDataReader reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                return true;
            }
            throw new Exception("User is not present in the application. Please sync and try again or contact System Administrator");
        }

        // returns the list of access details the User has
        private StrList getAccessDetailsList(String userId)
        {
            Log("DEBUG", "Getting access details list");
            StrList accessDtls = new StrList();
            try
            {
                AseCommand cmd = new AseCommand("select distinct u.firstname,ug.UserId,g.groupdesc,a.ApplicationID, a.ApplicationDesc from EACS_UserGroup ug,EACS_user u,EACS_Group g,EACS_Application a," +
                                   "EACS_FunctionGroup fg,EACS_Function f where ug.UserID = '" + userId + "' " +
                                   " AND ug.Status='A' and ug.UserID = u.userId and g.groupId = ug.groupId and " +
                                   " a.ApplicationID = f.ApplicationID and fg.FunctionID = f.FunctionID and fg.GroupID = ug.GroupID", this.connection);
                AseDataReader reader = cmd.ExecuteReader();
                Log("INFO", "Access details query :: " + cmd.CommandText);
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        accessDtls.Add(reader["GroupDesc"] + " - " + reader["ApplicationID"] + " - " + reader["ApplicationDesc"] + " - " + reader["groupdesc"]);
                    }
                }
            }
            catch (Exception e)
            {
                Log("ERROR","Error while fetching access details for user "+userId+" Error Message "+e.Message);
                throw new Exception("Failed to fetch Access details for User Please Contact System Administrator");
            }            
            Log("DEBUG", "Returning access details list "+ accessDtls.Count);
            return accessDtls;
        }

        // get User Group List
        private StrList getUserGroupsList(String userId)
        {
            Log("DEBUG", "Getting User group list");
            StrList grpList = new StrList();
            try
            {
                AseCommand cmd = new AseCommand("SELECT GroupId FROM EACS_UserGroup WHERE UserID = '" + userId + "' AND Status = 'A'", this.connection);
                Log("INFO", "Groups query :: " + cmd.CommandText);
                AseDataReader reader = cmd.ExecuteReader();

                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        grpList.Add(reader["GroupId"].ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while fetching user group list " + userId + " Error Message " + e.Message);
                throw new Exception("Failed to fetch User Group List. Please Contact System Administrator");
            }
            Log("DEBUG", "Returning access details list " + grpList.Count);
            return grpList;
        }

        // deleting the present groups. This is done before adding the new groups.
        private void deleteExistingGroups(String userId,Boolean isDisable)
        {
            Log("DEBUG", "deleting existing group list for user :: "+userId);
            StrList grpList = new StrList();
            try
            {
                AseCommand cmd = new AseCommand("SELECT GroupId FROM EACS_UserGroup WHERE UserID = '" + userId + "' AND Status = 'A'", this.connection);
                Log("INFO", "Groups query :: " + cmd.CommandText);
                AseDataReader reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    String currGrp = null;
                    while (reader.Read())
                    {
                        currGrp = reader["GroupId"].ToString();
                        Log("INFO", "Deleting group " + currGrp);
                        executeCommand("UPDATE EACS_UserGroup SET status = 'D', access = 'Deleted' WHERE UserId = '" + userId + "' AND GroupID='" + currGrp + "'", null, "ADIBAMSCnctr_AcctCreate");
                        executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, OldValue, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + userId + "-" + currGrp + "',0,'Access','Active','Closed','U','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Access','0')", null, "ADIBAMSCnctr_AcctCreate");
                    }
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while fetching user group list " + userId + " Error Message " + e.Message);
                throw new Exception("Failed to fetch User Group List. Please Contact System Administrator");
            }
            Log("DEBUG", "Returning after deleting all the existing groups ");
        }

        public void ADIBAMSCnctr_ValidateTargetConfig(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAMSCnctr_ValidateTargetConfig ===============");
            try
            {
                // Setup the target parameters
                setupConfig(reqObj);
                Log("INFO", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAMSCnctr_ValidateTargetConfig");
                    Log("INFO", "Target validated successfully.");
                    closeConnection("ADIBAMSCnctr_ValidateTargetConfig");
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + "EXCEPTION");
                }

            }
            catch (Exception ex)
            {
                SetExceptionMessage(ex);
                Log("INFO", "Target validation failed.");
            }
            finally
            {
                Log("INFO", "=============== Out ADIBAMSCnctr_ValidateTargetConfig ===============");
                respond_validateTargetConfiguration(resObj, this.bErr, this.sErrMsg);
            }
        } // ADIBAMSCnctr_ValidateTargetConfig

        public void ADIBAMSCnctr_AcctInfo(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                // Respond with not supported
                respond_statusNotSupported(resObj);
            }
            else
            {
                CourParametersObject mapAttrsToValues = new CourParametersObject();
                Courion.Types.StrList lstNotAllowed = new Courion.Types.StrList();
                try
                {
                    Log("INFO", "=============== In ADIBAMSCnctr_AcctInfo ===============");
                    // Setup the target parameters
                    setupConfig(reqObj);
                    Log("DEBUG", "Request XML from CCM: " + reqObj.xmlDoc); // TODO: Should never be added.
                    // Initialize the username
                    Log("DEBUG", "UserName: " + reqObj.m_accountName);

                    makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAMSCnctr_AcctInfo");
                    // adding basic details

                    // initilizing the attributes
                    initializeAttributes(reqObj, "ADIBAMSCnctr_AcctInfo");
                    
                    // adding access details for User to show
                    mapAttrsToValues.SetParamValues("AccessDetails", getAccessDetailsList(reqObj.m_accountName));

                    // adding User group list
                    mapAttrsToValues.SetParamValues("Groups", getUserGroupsList(reqObj.m_accountName));
                    
                    Log("Account: " + reqObj.m_accountName + " fetched successfully.");
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                }
                finally
                {
                    Log("=============== Out ADIBAMSCnctr_AcctInfo ===============");
                    respond_acctInfo(resObj, mapAttrsToValues, lstNotAllowed, this.bErr, this.sErrMsg);
                }
            }
        }// ADIBAMSCnctr_AcctInfo

        public void ADIBAMSCnctr_AcctCreate(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAMSCnctr_AcctCreate ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                respond_statusNotSupported(resObj);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAMSCnctr_AcctCreate");
                    Log("Request received to create AMS User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBAMSCnctr_AcctCreate");
                    if (checkUserPresent(reqObj))
                    {
                        if (this.m_sGroups.Equals(""))
                        {
                            throw new Exception("Atleast 1 group needs to be provisioned");
                        }
                        String[] groups = this.m_sGroups.Split(multiValueSeprator);
                        if (groups.Length == 0)
                        {
                            groups = new String[] { this.m_sGroups};
                        }                        
                        AseCommand usrGrpCmd = null;
                        AseDataReader usrGrpReader = null;
                        foreach (String group in groups)
                        {
                            usrGrpCmd = new AseCommand("SELECT * FROM EACS_UserGroup WHERE UserID = '" + reqObj.m_accountName + "' and GroupID = '" + group + "'", this.connection);
                            usrGrpReader = usrGrpCmd.ExecuteReader();
                            if (usrGrpReader.HasRows)
                            {
                                Log("DEBUG","Group " + group + " already exist for User " + reqObj.m_accountName + " updating...");
                                executeCommand("UPDATE EACS_UserGroup SET status = 'A', access = 'Active' WHERE UserId = '" + reqObj.m_accountName + "' AND GroupID='" + group + "'", null, "ADIBAMSCnctr_AcctCreate");
                                executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'GroupID','" + group + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                                executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'UserID','" + reqObj.m_accountName + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                            }
                            else
                            {
                                Log("DEBUG","Group " + group + " doesn't exist for User " + reqObj.m_accountName + " adding...");
                                executeCommand("INSERT INTO EACS_UserGroup (UserID,GroupID,Status,Access) values ('" + reqObj.m_accountName + "','" + group + "','A','Active')", null, "ADIBAMSCnctr_AcctCreate");
                                executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'GroupID','" + group + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                                executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'UserID','" + reqObj.m_accountName + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                            }
                            usrGrpReader.Dispose();
                            usrGrpCmd.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBAMSCnctr_AcctCreate");
                    Log("DEBUG", "=============== Out ADIBAMSCnctr_AcctCreate ===============");
                    respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }

            }
        } // ADIBAMSCnctr_AcctCreate

        public void ADIBAMSCnctr_AcctChange(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                // Respond with not supported
                respond_statusNotSupported(resObj);
            }
            else
            {
                try
                {
                    string sResult = string.Empty;
                    string sUnlockOnly = string.Empty;
                    if (reqObj.m_object == "Password Reset")
                    {
                        respond_statusNotSupported(resObj);
                    }
                    else // Perform change action
                    {
                        Log("DEBUG", "=============== In ADIBAMSCnctr_AcctChange ===============");
                        setupConfig(reqObj);
                        Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                        try
                        {
                            makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAMSCnctr_AcctChange");
                            Log("Request received to create AMS User : " + reqObj.m_accountName);
                            initializeAttributes(reqObj, "ADIBAMSCnctr_AcctChange");
                            // update previous groups to delete
                            deleteExistingGroups(reqObj.m_accountName,false);
                            Log("INFO", "groups :: " + this.m_sGroups);
                            if (checkUserPresent(reqObj))
                            {
                                if (this.m_sGroups.Equals(""))
                                {
                                    throw new Exception("Atleast 1 group needs to be provisioned");
                                }
                                String[] groups = this.m_sGroups.Split(multiValueSeprator);
                                if (groups.Length == 0)
                                {
                                    groups = new String[] { this.m_sGroups };
                                }
                                AseCommand usrGrpCmd = null;
                                AseDataReader usrGrpReader = null;
                                foreach (String group in groups)
                                {
                                    usrGrpCmd = new AseCommand("SELECT * FROM EACS_UserGroup WHERE UserID = '" + reqObj.m_accountName + "' and GroupID = '" + group + "'", this.connection);
                                    usrGrpReader = usrGrpCmd.ExecuteReader();
                                    if (usrGrpReader.HasRows)
                                    {
                                        Log("DEBUG", "Group " + group + " already exist for User " + reqObj.m_accountName + " updating...");
                                        executeCommand("UPDATE EACS_UserGroup SET status = 'A', access = 'Active' WHERE UserId = '" + reqObj.m_accountName + "' AND GroupID='" + group + "'", null, "ADIBAMSCnctr_AcctCreate");
                                        executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'GroupID','" + group + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                                        executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'UserID','" + reqObj.m_accountName + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                                    }
                                    else
                                    {
                                        Log("DEBUG", "Group " + group + " doesn't exist for User " + reqObj.m_accountName + " adding...");
                                        executeCommand("INSERT INTO EACS_UserGroup (UserID,GroupID,Status,Access) values ('" + reqObj.m_accountName + "','" + group + "','A','Active')", null, "ADIBAMSCnctr_AcctCreate");
                                        executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'GroupID','" + group + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                                        executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'UserID','" + reqObj.m_accountName + "','I','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Group ID','0')", null, "ADIBAMSCnctr_AcctCreate");
                                    }
                                    usrGrpReader.Dispose();
                                    usrGrpCmd.Dispose();
                                }
                            }                            
                        }
                        catch (Exception ex)
                        {
                            SetExceptionMessage(ex);
                        }
                        finally
                        {
                            Log("DEBUG", "=============== Out ADIBAMSCnctr_AcctChange ===============");
                            closeConnection("ADIBAMSCnctr_AcctChange");
                            respond_acctChange(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                    Log("INFO", "Modifying account details failed.");
                }
                finally
                {
                    Log("DEBUG", "=============== Out ADIBAMSCnctr_AcctChange ===============");
                    closeConnection("ADIBAMSCnctr_AcctChange");
                    respond_acctChange(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBAMSCnctr_AcctChange

        public void ADIBAMSCnctr_AcctEnable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAMSCnctr_AcctEnable ===============");
            respond_statusNotSupported(resObj);
            Log("INFO", "=============== Out ADIBAMSCnctr_AcctEnable ===============");
        } // ADIBAMSCnctr_AcctEnable

        public void ADIBAMSCnctr_AcctDisable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                // Respond with not supported
                respond_statusNotSupported(resObj);
            }
            else
            {               
                Log("DEBUG", "=============== In ADIBAMSCnctr_AcctDisable ===============");
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAMSCnctr_AcctDisable");
                    Log("Request received to create AMS User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBAMSCnctr_AcctDisable");
                    // update previous groups to delete
                    deleteExistingGroups(reqObj.m_accountName,true);
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                }
                finally
                {
                    Log("DEBUG", "=============== Out ADIBAMSCnctr_AcctDisable ===============");
                    closeConnection("ADIBAMSCnctr_AcctDisable");
                    respond_acctChange(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }                
            }
        } // ADIBAMSCnctr_AcctDisable

        public void ADIBAMSCnctr_AcctDelete(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAMSCnctr_AcctDELETE ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                respond_statusNotSupported(resObj);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAMSCnctr_AcctDELETE");
                    Log("Request received to delete AMS User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBAMSCnctr_AcctDELETE");
                    if (checkUserPresent(reqObj))
                    {
                        if (this.m_sGroups.Equals(""))
                        {
                            throw new Exception("Atleast 1 group needs to be provisioned");
                        }
                        String[] groups = this.m_sGroups.Split(multiValueSeprator);
                        if (groups.Length == 0)
                        {
                            groups = new String[] { this.m_sGroups };
                        }
                        AseCommand usrGrpCmd = null;
                        AseDataReader usrGrpReader = null;
                        foreach (String group in groups)
                        {
                            usrGrpCmd = new AseCommand("SELECT * FROM EACS_UserGroup WHERE UserID = '" + reqObj.m_accountName + "' and GroupID = '" + group + "'", this.connection);
                            usrGrpReader = usrGrpCmd.ExecuteReader();
                            if (usrGrpReader.HasRows)
                            {
                                Log("DEBUG", "Group " + group + " already exist for User " + reqObj.m_accountName + " updating...");
                                executeCommand("UPDATE EACS_UserGroup SET status = 'D', access = 'Deleted' WHERE UserId = '" + reqObj.m_accountName + "' AND GroupID='" + group + "'", null, "ADIBAMSCnctr_AcctCreate");
                                executeCommand("INSERT INTO EACS_TransactionLog (ScreenID, RowID, FieldName, OldValue, NewValue, TranType,CreatedBy,CreatedDate,ApprovedBy,ApprovedDate, Status,TableName, DisplayFieldName, PreApproval) VALUES ('UserGroup/" + reqObj.m_accountName + "-" + group + "',0,'Access','Active','Closed','U','888880','" + DateTime.Now + "','888880','" + DateTime.Now + "','C','EACS_UserGroup','Access','0')", null, "ADIBAMSCnctr_AcctDELETE");
                            }
                            usrGrpReader.Dispose();
                            usrGrpCmd.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBAMSCnctr_AcctDELETE");
                    Log("DEBUG", "=============== Out ADIBAMSCnctr_AcctDELETE ===============");
                    respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBAMSCnctr_AcctDELETE

        public override void AssignSupportedScriptFunctions()
        {
            base.RedirectInterface(COUR_INTERFACE_VALIDATE_TARGET_CONFIG, ADIBAMSCnctr_ValidateTargetConfig, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CREATE, ADIBAMSCnctr_AcctCreate, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CHANGE, ADIBAMSCnctr_AcctChange, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_INFO, ADIBAMSCnctr_AcctInfo, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DISABLE, ADIBAMSCnctr_AcctDisable, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_ENABLE, ADIBAMSCnctr_AcctEnable, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DELETE, ADIBAMSCnctr_AcctDelete, true, true, false);
        }

    }
}
