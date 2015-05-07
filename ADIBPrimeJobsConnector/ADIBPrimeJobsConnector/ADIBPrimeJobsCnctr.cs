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
// Author: Ashish Joshi
// Date: 07 May 2015
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
using System.Data.OracleClient;

namespace ADIBPrimeJobsConnector
{
    public class ADIBPrimeJobsCnctr : RDKCore
    {
        // Target Validation Parameters
        private string m_sHost;
        private string m_sPort;
        private string m_sServiceName;
        private string m_sDBUserName;
        private string m_sDBPassword;
        

        // connection objects
        private OracleConnection connection;        
        private OracleCommand command;

        // Operational Parameters
        private string m_sUsername;
        private string m_sPassword;
        private string m_sFirstname;
        private string m_sLastname;
        private string m_sOrganization;
        private string m_sMonitor;

        // PMM Parameters
        private bool m_bUnlockOnly;
        private bool m_bForceChangeAtNextLogon;

        private int intMonitor = -1;
        private int intInstAccess = -1;

        // Supporting variables
        private bool bErr = false;
        private string sErrMsg;
        private string sResult;

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



        public ADIBPrimeJobsCnctr()
            : base("CourionPrimeJobsCnctr")
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
                attributesFile = !string.IsNullOrEmpty(config.AppSettings.Settings["AttributeXML"].Value) ? config.AppSettings.Settings["AttributeXML"].Value : "ADIBCSFAttributes.xml";
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
                this.m_sServiceName = req.GetParameter("ServiceName");
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
        private void makeConnection(String host, String port, String serviceName, String username, String password,String methodName) 
        {
            Log("DEBUG", "Trying to establish connection to the PrimeJobs database for method " + methodName);
            Log("DEBUG", "retryCount = "+iRetryCount + " connect timout= "+iConnectionTimeout);
            //Log("DEBUG", "Parameters host : " + host + " port : "+port + " serviceName : "+serviceName + " username : "+username +" password : "+password);  
            this.connection = new OracleConnection();
            try
            {
                String connString = "user id="+username+";password="+password+";data source=" +
                    "(DESCRIPTION=(CONNECT_TIMEOUT="+iConnectionTimeout+")(RETRY_COUNT="+iRetryCount+")(ADDRESS=(PROTOCOL=tcp)" +
                    "(HOST="+host+")(PORT="+port+"))(CONNECT_DATA=" +
                    "(SERVICE_NAME="+serviceName+")))";

                connection.ConnectionString = connString;

                connection.Open();
                Log("INFO","Successfully connected to the PrimeJobs database for method " + methodName);                
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to PrimeJobs database for method " + methodName + " :: " + e.Message);
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
                Log("ERROR", "Error while connecting to PrimeJobs database :: " + e.Message);
                throw e;
            }

        }

        // This method is creates a Oracle Command
        private void executeCommand(String query,Dictionary<String,Object> parameters,String methodName)
        {   
            try
            {
                this.command = new OracleCommand(query, this.connection);
                command.ExecuteNonQuery();                
            }
            catch (Exception e)
            {
                Log("DEBUG", "executeCommand called failed " + query + " for method :: " + e.Message);
                Log("ERROR", "executeCommand called failed for method :: " + e.Message);
                throw e;
            }

        }

        // initialize the attributes
        private void initializeAttributes(RequestObject reqObj, String methodName)
        {
            Log("DEBUG", "Initializing the attributes for method " + methodName);
            String prmjobsCentral = reqObj.GetParameter("GROUPS-CENTRAL");
            String prmjobsEgypt = reqObj.GetParameter("GROUPS-EGYPT");
            String prmjobsUAE = reqObj.GetParameter("GROUPS-UAE");
            Log("UserID in Courion : " + reqObj.GetParameter("UserID"));
            this.m_sPassword = reqObj.GetParameter("Password");

            int countNull = 0;
            if (prmjobsCentral.Equals("") == false)
            {
                countNull++;
            }
            if (prmjobsEgypt.Equals("") == false)
            {
                countNull++;
            }
            if (prmjobsUAE.Equals("") == false)
            {
                countNull++;
            }

            if (countNull > 1)
            {
                throw new Exception("More than one Locations can't be supported. PLease select only one Location.");
            }

            String listofRoles = "";

            if (prmjobsCentral.Equals("") == false)
            {
                this.m_sUsername = "C" + reqObj.GetParameter("UserID");
                listofRoles = prmjobsCentral;
            }
            else if (prmjobsEgypt.Equals("") == false)
            {
                this.m_sUsername = "E" + reqObj.GetParameter("UserID");
                listofRoles = prmjobsEgypt;
            }
            else if (prmjobsUAE.Equals("") == false)
            {
                this.m_sUsername = "U" + reqObj.GetParameter("UserID");
                listofRoles = prmjobsUAE;
            }

            Log("User ID in PrimeJobs : " + this.m_sUsername);
            Log("List of Roles : " + listofRoles);

            //SELECT count(*) from dba_users where username = '"&strUserId&"'"
            OracleCommand selectQuery = new OracleCommand("SELECT count(*) from dba_users where username = '" + this.m_sUsername +"'", this.connection);
            OracleDataReader reader = selectQuery.ExecuteReader();
            while (reader.Read())
            {
                if (Int16.Parse(reader["count(*)"].ToString()) > 0)
                {
                    Log("User already exists in PrimeJobs.");
                }
                else
                {
                    Log("User " + this.m_sUsername + " Doesn't exists Creating.");
                    executeCommand("CREATE USER \"" + this.m_sUsername + "\" IDENTIFIED BY \"" + this.m_sPassword + "\"", null, "ADIBPrimeJobsCnctr_AcctCreate");
                    Log("User " + this.m_sUsername + "Created.");
                }
            }

            String[] arrayofRoles = listofRoles.Split(',');
            foreach(String role in arrayofRoles)
            {
                if (role.ToUpper().IndexOf("EODROLE") > 0)
                {
                    executeCommand("grant PRIME_EOD to " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctCreate");
                    executeCommand("grant PRIME_EOD_ROLE to " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctCreate");
                }
                Log("Assigning Role " + role + " to User " + this.m_sUsername);
                executeCommand("grant " + role + " to " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctCreate");
                Log("Assigned Role " + role + " to User " + this.m_sUsername);
            }

            executeCommand("grant connect to " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctCreate");
            executeCommand("grant Prime_select to " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctCreate");
            executeCommand("grant prime_jobs to " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctCreate");

            Log("Creating User " + this.m_sUsername);
            executeCommand("Insert into primeusers( institution_id, serno, username, usertype, schemaname, logaction) Values (0, s_primeusers.nextval, '" + this.m_sUsername + "', 'U','ADIB','Create')", null, "ADIBPrimeJobsCnctr_AcctCreate");
            Log("Created User " + this.m_sUsername);

            Log("DEBUG", "Attributes Initialized successfully for method " + methodName);
        }

        

        // This method converts the String into an Array Sorts the Values and then Joins them in UpperCase
        private String sortValues(String inputString,char seprator)
        {
            Log("DEBUG", "Sorting the Values " + inputString);
            String outputString = "";
            if (inputString != null && inputString != "")
            {
                string[] values = inputString.Split(seprator);
                Array.Sort(values);
                outputString = String.Join(seprator.ToString(), values);
            }
            Log("DEBUG", "Values after sorting " + outputString);
            return outputString.ToUpper();
        }

        // calculate the Value for Monitor and based on that GRANT the access to the database tables
        private void calculateMonitor()
        {            
            if ((this.m_sMonitor != null) || (this.m_sMonitor != ""))
            {
                string strMonitor = sortValues(this.m_sMonitor,',');
		        if (strMonitor == "CONTROL,MONITOR,VIEW") {
			        this.intMonitor = 17;
                    this.intInstAccess = 14;
		        }
		        else if (strMonitor == "MONITOR,VIEW") {
                    this.intMonitor = 16;
                    this.intInstAccess = 10;
		        }
		        else if (strMonitor == "CONTROL,VIEW") {
                    this.intMonitor = 17;
                    this.intInstAccess = 14;
		        }
		        else if (strMonitor == "CONTROL,MONITOR") {
                    this.intMonitor = 17;
                    this.intInstAccess = 6;
		        }
		        else if (strMonitor == "MONITOR") {
                    this.intMonitor = 16;
                    this.intInstAccess = 2;
		        }
		        else if (strMonitor == "CONTROL") {
                    this.intMonitor = 17;
                    this.intInstAccess = 6;
		        }
		        else if (strMonitor == "VIEW") {
                    this.intMonitor = 0;
                    this.intInstAccess = 8;
		        }
		        else if (strMonitor == "FULL") {
                    this.intMonitor = 255;
                    this.intInstAccess = 14;
		        }
	        }
            Log("INFO", "intMonitor=" + this.intMonitor + " :: intInstAccess=" + this.intInstAccess);
        }

        // insert data in the monitor access and inst access table and also provide access to the tables
        private void insertMonitorValues(Boolean changeMode)
        {
            if (this.intMonitor > -1 && this.intInstAccess > -1)
            {
                grantMonitorTablesAccess();
                Log("INFO", "Insert values in to inst_access based on the calculated value for Monitor");
                executeCommand("INSERT INTO INST_ACCESS VALUES ('" + this.m_sUsername + "',1,0)", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
                executeCommand("UPDATE INST_ACCESS SET INST_ACCESS = " + this.intInstAccess + " where user_id = '" + this.m_sUsername + "'", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");

                Log("INFO", "Insert values in to monitor_access based on the calculated value for Monitor");
                executeCommand("INSERT INTO MONITOR_ACCESS VALUES ('" + this.m_sUsername + "',0)", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
                executeCommand("UPDATE MONITOR_ACCESS SET MONITOR_ACCESS = " + this.intMonitor + " where user_id = '" + this.m_sUsername + "'", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            }
            else
            {
                if (changeMode)
                {
                    revokeMonitorTablesAccess();
                }
            }
        }

        // provides access to monitor tables
        private void grantMonitorTablesAccess()
        {
            Log("INFO", "Granting access for various tables");
            executeCommand("GRANT SELECT ON ACCOUNT_TYPE_DEF to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX  ON ATM to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT ON ATM_CONFIG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT ON ATM_SAT to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT,INSERT,UPDATE,DELETE,REFERENCES,ALTER,INDEX ON ATM_STATUS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on AUTHORIZER to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on CASH_SAT to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CMD_QUEUE to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on COLORINFO to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on COMM_HISTORY to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on CONFIG_INFO_NCR to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CURR_ATM_STATUS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on CURR_COMM_STATUS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on DEVICE to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on DEVICE_STATUS_DESC to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on EJ_LOG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on EMV_DATA to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on EMV_ELEMENTS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on ERROR_DESC to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on ERROR_LOG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on HDWARE_INFO_A91X to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on HDWARE_INFO_NCR to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on HSM_SETTINGS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on INST_ACCESS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on INSTITUTION to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on ISO8583_CONFIG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on LOG_DETAILS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on LOG_RECORD to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on LOG_RECORD_FIELDS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on LOG_RECORD_HIST to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on MERCHANT_TYPES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on MONITOR_ACCESS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on PROCESSES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on REENTRY_CODE_DESC to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on SECURITY_SETTINGS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on SENSOR_INFO_NCR to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on SN_STATUS_LOG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on SWX_STATUS_CODES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on SYSTEM_PARAMS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on TERMINAL_PROCESSOR to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on TIMER to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on TYPE_CODES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on USER_NAME to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("GRANT SELECT on VOID_CODES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            Log("INFO", "Access granted to moniter tables");
        }

        // revokes access from monitor tables this is used in case of Account Change
        private void revokeMonitorTablesAccess()
        {
            Log("INFO", "Since Monitor has no value selected revoking the grant to the tables.");
            executeCommand("REVOKE SELECT ON ACCOUNT_TYPE_DEF from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX  ON ATM from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT ON ATM_CONFIG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT ON ATM_SAT from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT,INSERT,UPDATE,DELETE,REFERENCES,ALTER,INDEX ON ATM_STATUS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on AUTHORIZER from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on CASH_SAT from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CMD_QUEUE from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on COLORINFO from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on COMM_HISTORY from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on CONFIG_INFO_NCR from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CURR_ATM_STATUS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on CURR_COMM_STATUS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on DEVICE from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on DEVICE_STATUS_DESC from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on EJ_LOG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on EMV_DATA from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on EMV_ELEMENTS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on ERROR_DESC from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on ERROR_LOG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on HDWARE_INFO_A91X from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on HDWARE_INFO_NCR from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on HSM_SETTINGS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on INST_ACCESS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on INSTITUTION from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on ISO8583_CONFIG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on LOG_DETAILS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on LOG_RECORD from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on LOG_RECORD_FIELDS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on LOG_RECORD_HIST from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on MERCHANT_TYPES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on MONITOR_ACCESS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on PROCESSES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on REENTRY_CODE_DESC from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on SECURITY_SETTINGS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on SENSOR_INFO_NCR from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on SN_STATUS_LOG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on SWX_STATUS_CODES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on SYSTEM_PARAMS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on TERMINAL_PROCESSOR from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on TIMER from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on TYPE_CODES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on USER_NAME from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            executeCommand("REVOKE SELECT on VOID_CODES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate_CalculateMonitor");
            Log("INFO", "Access revoked from the monitor tables.");
        }

        // calculate the value for each CCM
        private int calculateCCM(String sCCMName,String sValue)        
        {
            Log("INFO", "Inside calculate CMM for " + sCCMName);
	        int intCCM = -1;
            if ((sValue != null) || (sValue != ""))
            {
                string strCCM = sortValues(sValue, ',');
		
		        if (strCCM == "INQUIRY,MODIFY,STATUS") {
                    intCCM = 2051;
		        }
		        else if (strCCM == "INQUIRY,MODIFY") {
			        intCCM = 3;
		        }
		        else if (strCCM == "INQUIRY,STATUS") {
			        intCCM = 2049;
		        }
		        else if (strCCM == "MODIFY,STATUS") {
			        intCCM = 2050;
		        }
		        else if (strCCM == "INQUIRY") {
			        intCCM = 1;
		        }
		        else if (strCCM == "MODIFY") {
			        intCCM = 2;
		        }
		        else if (strCCM == "STATUS") {
			        intCCM = 2048;
		        }
	        }
            Log("INFO", "Calculated CMM for " + sCCMName + " is " + intCCM);
	        return intCCM;
        }

        // grant access to the BIN tables
        private void grantBINTableAccess()
        {
            Log("INFO", "Granting access to the BIN tables");
            executeCommand("GRANT SELECT, INSERT, update, delete, references, alter, index on ACCOUNT to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on ACCOUNT_LOG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on ACCOUNT_STATUS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on ACCOUNT_TYPE to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on ACCOUNT_TYPE_DEF to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on AUTHORIZER to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on BIN_EMV_CRM_TAGS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on BRANCH_DESC to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on CARD_CLASSES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER_EMV to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on CARDHOLDER_LOG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER_NAME to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on CARDHOLDER_NAME_LG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on CARDHOLDER_QUERY to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on CARDHOLDER_STATUS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER_TRAN to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on CARD_ISSUE to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on CCA_ACCESS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CCA_PARAMS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on EMV_CARD_SCRIPTS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on EMV_CMD_MACING to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on EMV_DATA to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on FX_CURRENCY to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on INSTITUTION to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on ISO8583_CONFIG to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on LANGUAGE to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on LOG_RECORD to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on LOG_RECORD_HIST to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on STANDBY_AUTH to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on SWITCH_NEGATIVE to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on SYSTEM_PARAMS to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on TRAN_CODES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on USER_NAME to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT SELECT on VOID_CODES to \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CARD_STATUS_REASON TO\"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CARDHOLDER_COUNTRY TO\"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CARDHOLDER_EMV_LOG TO\"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CCA_PARAMS2 TO\"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.USER_ACCESS_LOG TO\"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("GRANT ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.USER_NAME TO\"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            Log("INFO", "Access Granted to BIN tables");
        }

        // revoke access from the BIN tables
        private void revokeBINTableAccess()
        {
            Log("INFO", "Revoking access to the BIN tables");
            executeCommand("REVOKE SELECT, INSERT, update, delete, references, alter, index on ACCOUNT from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on ACCOUNT_LOG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on ACCOUNT_STATUS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on ACCOUNT_TYPE from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on ACCOUNT_TYPE_DEF from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on AUTHORIZER from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on BIN_EMV_CRM_TAGS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on BRANCH_DESC from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on CARD_CLASSES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER_EMV from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on CARDHOLDER_LOG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER_NAME from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on CARDHOLDER_NAME_LG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on CARDHOLDER_QUERY from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on CARDHOLDER_STATUS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CARDHOLDER_TRAN from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on CARD_ISSUE from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on CCA_ACCESS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on CCA_PARAMS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on EMV_CARD_SCRIPTS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on EMV_CMD_MACING from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on EMV_DATA from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on FX_CURRENCY from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on INSTITUTION from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on ISO8583_CONFIG from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on LANGUAGE from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on LOG_RECORD from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on LOG_RECORD_HIST from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on STANDBY_AUTH from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT, INSERT, UPDATE, DELETE, REFERENCES, ALTER, INDEX on SWITCH_NEGATIVE from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on SYSTEM_PARAMS from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on TRAN_CODES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on USER_NAME from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE SELECT on VOID_CODES from \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CARD_STATUS_REASON FROM  \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CARDHOLDER_COUNTRY FROM  \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CARDHOLDER_EMV_LOG FROM  \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.CCA_PARAMS2 FROM  \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.USER_ACCESS_LOG FROM  \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            executeCommand("REVOKE ALTER, DELETE, INDEX, INSERT, REFERENCES, SELECT, UPDATE, ON COMMIT REFRESH, QUERY REWRITE, DEBUG, FLASHBACK ON SWX.USER_NAME FROM  \"" + this.m_sUsername + "\"", null, "ADIBCSFCnctr_AcctCreate");
            Log("INFO", "Access Revoked from BIN tables");
        }

        // check weather the user for which the Action takes place exist or not
        private Boolean userExist(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user exist in the PrimeJobs system"); // TODO: Should never be added.
            // Initialize the username
            this.m_sUsername = reqObj.m_accountName;
            Log("DEBUG", "UserName: " + this.m_sUsername);
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "userExist");
            OracleCommand selectCmd = new OracleCommand("SELECT username, account_status FROM dba_users where username = '"+this.m_sUsername+"'", this.connection);
            OracleDataReader reader = null;
            reader = selectCmd.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    if (reader["USERNAME"].Equals(this.m_sUsername))
                    {
                        reader.Close();
                        closeConnection("userExist");
                        return true;
                    }
                }
            }
            reader.Close();
            closeConnection("userExist");
            return false;
        }

        // check weather the user is locked or unlocked
        private Boolean userLocked(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user is locked in the PrimeJobs system");
            // Initialize the username
            this.m_sUsername = reqObj.m_accountName;
            Log("DEBUG", "UserName: " + this.m_sUsername);
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "userLocked");
            OracleCommand selectCmd = new OracleCommand("SELECT username, account_status FROM dba_users where username = '" + this.m_sUsername + "'", this.connection);
            OracleDataReader reader = null;
            reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader["ACCOUNT_STATUS"].Equals("LOCKED"))
                {
                    reader.Close();
                    closeConnection("userLocked");
                    return true;
                }
            }
            reader.Close();
            closeConnection("userLocked");
            return false;
        }

        public void ADIBPrimeJobsCnctr_ValidateTargetConfig(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBPrimeJobsCnctr_ValidateTargetConfig ===============");
            try
            {
                // Setup the target parameters
                setupConfig(reqObj);
                Log("INFO", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_ValidateTargetConfig");               
                    Log("INFO", "Target validated successfully.");
                    closeConnection("ADIBPrimeJobsCnctr_ValidateTargetConfig");
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
                Log("INFO", "=============== Out ADIBPrimeJobsCnctr_ValidateTargetConfig ===============");
                respond_validateTargetConfiguration(resObj, this.bErr, this.sErrMsg);
            }
        } // ADIBPrimeJobsCnctr_ValidateTargetConfig

        public void ADIBPrimeJobsCnctr_AcctInfo(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                    Log("INFO", "=============== In ADIBPrimeJobsCnctr_AcctInfo ===============");
                    // Setup the target parameters
                    setupConfig(reqObj);
                    Log("DEBUG", "Request XML from CCM: " + reqObj.xmlDoc); // TODO: Should never be added.
                    // Initialize the username
                    this.m_sUsername = reqObj.m_accountName;
                    Log("DEBUG", "UserName: " + this.m_sUsername);

                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_AcctInfo");

                    OracleCommand selectCmd = new OracleCommand("select dba_users.username,dba_users.Account_status,dba_users.expiry_date,dba_role_privs.granted_role from dba_users,dba_role_privs where dba_role_privs.grantee=dba_users.username and dba_users.Account_status NOT like 'EXPIRED%' and dba_users.username NOT IN ('ADIB','ADIBARCH','ADIBCEN','ADIBCENTER','ADIBEGY','ADIBPARAM','ADIBUAE','CRYSTAL','DDM','IBRO','IVRRO','MISPRIMERO','PRIMEWEB','SYS','SYSTEM','USSDRO','WEBAPPRO') and dba_users.username = '"+ this.m_sUsername +"'", this.connection);
                    OracleDataReader reader = null;

                    reader = selectCmd.ExecuteReader();

                    StrList roles = new StrList();                    

                    while (reader.Read())
                    {
                        roles.Add(reader["GRANTED_ROLE"].ToString());
                    }                       
                    if (m_sUsername.ToString().ToUpper().StartsWith("E"))
                    {
                        mapAttrsToValues.SetParamValues("GROUPS-EGYPT", roles);
                        mapAttrsToValues.SetParamValues("GROUPS-CENTRAL", new StrList());
                        mapAttrsToValues.SetParamValues("GROUPS-UAE", new StrList()); 
                    }
                    else if (m_sUsername.ToString().ToUpper().StartsWith("C"))
                    {
                        mapAttrsToValues.SetParamValues("GROUPS-CENTRAL", roles);
                        mapAttrsToValues.SetParamValues("GROUPS-EGYPT", new StrList());
                        mapAttrsToValues.SetParamValues("GROUPS-UAE", new StrList());
                    }
                    else if (m_sUsername.ToString().ToUpper().StartsWith("U"))
                    {
                        mapAttrsToValues.SetParamValues("GROUPS-UAE", roles);
                        mapAttrsToValues.SetParamValues("GROUPS-EGYPT", new StrList());
                        mapAttrsToValues.SetParamValues("GROUPS-CENTRAL", new StrList());
                    }
                    reader.Close();
                    //Log("Account: " + this.m_sUsername + " fetched successfully.");
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                }
                finally
                {
                    Log("=============== Out ADIBPrimeJobsCnctr_AcctInfo ===============");
                    respond_acctInfo(resObj, mapAttrsToValues, lstNotAllowed, this.bErr, this.sErrMsg);
                }
            }
        }// ADIBCSFCnctr_AcctInfo

        public void ADIBPrimeJobsCnctr_AcctCreate(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBPrimeJobsCnctr_AcctCreate ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (userExist(reqObj))
                {
                    Log("DEBUG", "User already exist in the PrimeJobs system");
                    respond_acctCreate(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " already exist in the PrimeJobs system");
                }
                respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_AcctCreate");
                    this.m_sUsername = reqObj.m_accountName;
                    Log("Request received to create PrimeJobs User : " + this.m_sUsername);
                    initializeAttributes(reqObj, "ADIBPrimeJobsCnctr_AcctCreate");

                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBPrimeJobsCnctr_AcctCreate");
                    Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctCreate ===============");
                    respond_acctCreate(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }

            }            
        } // ADIBPrimeJobsCnctr_AcctCreate

        public void ADIBPrimeJobsCnctr_AcctChange(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                        // Read the configured value of 'UnlockOnly'. If it is not set to 'true' or 'false' throw exception
                        if (reqObj.GetParameter("UnlockOnly") == null)
                            throw new Exception("'UnlockOnly' is not configured.");

                        sUnlockOnly = reqObj.GetParameter("UnlockOnly");

                        Log(sUnlockOnly);

                        if ((sUnlockOnly.ToUpper() != "TRUE") && (sUnlockOnly.ToUpper() != "FALSE"))
                            throw new Exception("Invalid value configured for 'UnlockOnly'. It must either be 'true' or 'false'.");

                        this.m_bUnlockOnly = bool.Parse(sUnlockOnly);
                        if (this.m_bUnlockOnly)
                        {
                            Log("DEBUG", "=============== In ADIBCSFCnctr_AcctUnlock ===============");
                        }
                        else
                        {
                            Log("DEBUG", "=============== In ADIBCSFCnctr_AcctReset ===============");
                        }
                    }
                    else
                    {
                        Log("DEBUG", "=============== In ADIBCSFCnctr_AcctChange ===============");
                    }

                    // Setup the target parameters
                    setupConfig(reqObj);

                    // Initialize the username
                    this.m_sUsername = reqObj.m_accountName;

                    // making connection with the database
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBCSFCnctr_AcctChange");
                    

                    if (reqObj.m_object == "Password Reset")
                    {
                        if (this.m_bUnlockOnly)
                        {
                            // Only Unlock the account. No password reset performed.
                            // Set not required parameters to default value, as they will be required to build the XML
                            this.m_bForceChangeAtNextLogon = false;
                            String query = "ALTER USER \"" + this.m_sUsername + "\" ACCOUNT UNLOCK";
                            executeCommand(query, null, "ADIBCSFCnctr_AcctUnlock");
                        }
                        else
                        {
                            // Perform Self Service or Help Desk Password Reset
                            // Read the configured value of 'ForcePasswordChangeAtNextLogon'. If it is not configured throw exception
                            if (reqObj.GetParameter("ForcePasswordChangeAtNextLogon") == null)
                                throw new Exception("'ForcePasswordChangeAtNextLogon' is not configured.");

                            string sForceChange = reqObj.GetParameter("ForcePasswordChangeAtNextLogon");
                            // If it is not set to 'true' or 'false' throw exception
                            if ((sForceChange.ToUpper() != "TRUE") && (sForceChange.ToUpper() != "FALSE"))
                                throw new Exception("Invalid value configured for 'ForcePasswordChangeAtNextLogon'. It must either be 'true' or 'false'.");

                            this.m_bForceChangeAtNextLogon = Convert.ToBoolean(sForceChange);

                            // Read the new password of the account from the request
                            this.m_sPassword = reqObj.GetParameter("NewPassword");

                            if (this.m_bForceChangeAtNextLogon)
                            {
                                String query = "ALTER USER \"" + this.m_sUsername + "\" IDENTIFIED BY \"" + this.m_sPassword + "\" PASSWORD EXPIRE";
                                executeCommand(query, null, "ADIBCSFCnctr_PasswordResetHelpDesk");
                                Log("INFO", "Password reset for user, " + this.m_sUsername + " successfully performed using Help Desk Reset.");
                            }
                            else
                            {
                                String query = "ALTER USER \"" + this.m_sUsername + "\" IDENTIFIED BY \"" + this.m_sPassword + "\"";
                                executeCommand(query, null, "ADIBCSFCnctr_PasswordResetSelfService");
                                Log("INFO", "Password reset for user, " + this.m_sUsername + " successfully performed using Self-Service Reset.");
                            }
                        }
                    }
                    else // Perform change action
                    {                        
                        try
                        {/*
                            setupConfig(reqObj);
                            Log("DEBUG", "=============== In ADIBCSFCnctr_AcctChange ===============");
                            // Setup the target parameters
                            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBCSFCnctr_AcctChange");
                            this.m_sUsername = reqObj.m_accountName;
                            Log("Request received to modify CSF User : " + reqObj.m_accountName);
                            initializeAttributes(reqObj, "ADIBCSFCnctr_AcctChange");
                            //checkMonitorBinSelection();
                            executeCommand("update user_name set user_name= '" + this.m_sFirstname + " " + this.m_sLastname + "',dept = '" + this.m_sOrganization + "' where user_id ='" + this.m_sUsername + "'", null, "ADIBCSFCnctr_AcctChange");

                            executeCommand("delete from inst_access where user_id = '" + this.m_sUsername + "'", null, "ADIBCSFCnctr_AcctChange");
                            executeCommand("delete from monitor_access where user_id = '" + this.m_sUsername + "'", null, "ADIBCSFCnctr_AcctChange");

                            Log("INFO", "Data deleting from inst_access and monitor_access");

                            calculateMonitor();

                            insertMonitorValues(true);

                            executeCommand("DELETE FROM cca_access WHERE user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctChange");
                            Log("INFO", "Data deteted from cca_access");

                            executeCommand("DELETE FROM swx.adib_user WHERE ch_user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctChange");
                            Log("INFO", "Data deteted from swx.adib_user");



                            int iCCM_433367 = calculateCCM("CCM_433367",this.m_sCCM_433367);
                            int iCCM_471367 = calculateCCM("CCM_471367", this.m_sCCM_471367);
                            int iCCM_471368 = calculateCCM("CCM_471368", this.m_sCCM_471368);
                            int iCCM_425893 = calculateCCM("CCM_425893", this.m_sCCM_425893);
                            int iCCM_471366 = calculateCCM("CCM_471366", this.m_sCCM_471366);
                            int iCCM_457228 = calculateCCM("CCM_457228", this.m_sCCM_457228);
                            int iCCM_445543 = calculateCCM("CCM_445543", this.m_sCCM_445543);

                            if ((iCCM_433367 <= -1) && (iCCM_471367 <= -1) && (iCCM_471368 <= -1) && (iCCM_425893 <= -1) && (iCCM_471366 <= -1) && (iCCM_457228 <= -1) && (iCCM_445543 <= -1))
                            {
                                Log("INFO", "No CCM specified");
                                try
                                {
                                    revokeBINTableAccess();
                                }
                                catch (Exception e)
                                {
                                    Log("INFO", e.Message);
                                }
                            }
                            else 
                            {
                               grantBINTableAccess();
                               OracleCommand selectCmd = new OracleCommand("SELECT BIN FROM CARD_ISSUE t WHERE BIN NOT IN (888777,999999)", this.connection);
                               OracleDataReader reader = null;

                               reader = selectCmd.ExecuteReader();

                               while (reader.Read())
                               {
                                   executeCommand("INSERT INTO CCA_ACCESS VALUES ('" + this.m_sUsername + "'," + reader["BIN"] + ",0)", null, "ADIBCSFCnctr_AcctCreate");                           
                               }
                               reader.Close();

                                if (iCCM_433367 > -1) 
                                {
				                    Log("INFO","Update cca_access for iCCM_433367");			
				                    executeCommand("UPDATE CCA_ACCESS SET CCA_ACCESS = "+iCCM_433367+ " where USER_ID='" +this.m_sUsername+ "' and BIN IN (433367,445543)",null,"ADIBCSFCnctr_AcctCreate");
                                    if ((iCCM_433367 == 2048) || (iCCM_433367 == 2049) || (iCCM_433367 == 2050) || (iCCM_433367 == 2051))
                                    {
                                        //TODO: find DeptCode for the specified Organization value
                                        Log("INFO", "Organization is numeric so lets insert into swx.aib_user");
                                        Log("INFO", "Updating SWX.ADIB_USER value");
                                        executeCommand("INSERT INTO SWX.ADIB_USER VALUES ('" + this.m_sUsername + "','" + this.m_sFirstname + " " + this.m_sLastname + "','" + this.m_sOrganization + "')", null, "ADIBCSFCnctr_AcctChange");
                                    }
                                    else
                                    {
                                        Log("INFO", "Deleting value from table swx.adib_user");
                                        executeCommand("DELETE FROM swx.adib_user WHERE ch_user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctChange");
                                    }
			                    }

                                if (iCCM_471366 > -1)
                                {
                                    Log("INFO","Update cca_access for iCCM_471366");
                                    executeCommand("UPDATE CCA_ACCESS SET CCA_ACCESS = " + iCCM_471366 + " where USER_ID='" + this.m_sUsername + "' and BIN=471366", null, "ADIBCSFCnctr_AcctCreate");                            
                                }

                                if (iCCM_471367 > -1)
                                {
                                    Log("INFO", "Update cca_access for iCCM_471367");
                                    executeCommand("UPDATE CCA_ACCESS SET CCA_ACCESS = " + iCCM_471367 + " where USER_ID='" + this.m_sUsername + "' and BIN=471367", null, "ADIBCSFCnctr_AcctCreate");
                                }

                                if (iCCM_471368 > -1)
                                {
                                    Log("INFO", "Update cca_access for iCCM_471368");
                                    executeCommand("UPDATE CCA_ACCESS SET CCA_ACCESS = " + iCCM_471368 + " where USER_ID='" + this.m_sUsername + "' and BIN=471368", null, "ADIBCSFCnctr_AcctCreate");
                                }

                                if (iCCM_425893 > -1)
                                {
                                    Log("INFO", "Update cca_access for iCCM_425893");
                                    executeCommand("UPDATE CCA_ACCESS SET CCA_ACCESS = " + iCCM_425893 + " where USER_ID='" + this.m_sUsername + "' and BIN=425893", null, "ADIBCSFCnctr_AcctCreate");
                                }

                                if (iCCM_457228 > -1)
                                {
                                    Log("INFO", "Update cca_access for iCCM_457228");
                                    executeCommand("UPDATE CCA_ACCESS SET CCA_ACCESS = " + iCCM_457228 + " where USER_ID='" + this.m_sUsername + "' and BIN=457228", null, "ADIBCSFCnctr_AcctCreate");
                                }

                                if (iCCM_445543 > -1)
                                {
                                    Log("INFO", "Update cca_access for iCCM_445543");
                                    executeCommand("UPDATE CCA_ACCESS SET CCA_ACCESS = " + iCCM_445543 + " where USER_ID='" + this.m_sUsername + "' and BIN=445543", null, "ADIBCSFCnctr_AcctCreate");
                                }

                            }*/
                        }
                        catch (Exception ex)
                        {
                            SetExceptionMessage(ex);
                        }
                        finally
                        {
                            Log("DEBUG", "=============== Out ADIBCSFCnctr_AcctChange ===============");
                            closeConnection("ADIBCSFCnctr_AcctChange");
                            respond_acctChange(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                        }
                         
                    }
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);

                    if (reqObj.m_object == "Password Reset")
                    {
                        string param = reqObj.GetParameter("UnlockOnly");
                        if (param == "true")
                        {
                            Log("INFO", "Unlocking account failed.");
                        }
                        else
                        {
                            Log("INFO", "Resetting account password failed.");
                        }
                    }
                    else
                    {
                        Log("INFO", "Modifying account details failed.");
                    }
                }
                finally
                {
                    if (reqObj.m_object == "Password Reset")
                    {
                        if (this.m_bUnlockOnly)
                        {
                            Log("DEBUG", "=============== Out ADIBCSFCnctr_AcctUnlock ===============");
                            closeConnection("ADIBCSFCnctr_AcctUnlock");
                        }
                        else
                        {
                            Log("DEBUG", "=============== Out ADIBCSFCnctr_AcctReset ===============");
                            closeConnection("ADIBCSFCnctr_AcctReset");
                        }
                    }
                    else
                    {
                        Log("DEBUG", "=============== Out ADIBCSFCnctr_AcctChange ===============");
                        closeConnection("ADIBCSFCnctr_AcctChange");
                    }                    
                    respond_acctChange(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBPrimeJobsCnctr_AcctChange

        public void ADIBPrimeJobsCnctr_AcctEnable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBPrimeJobsCnctr_AcctEnable ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (!userExist(reqObj))
                {
                    Log("INFO", "User doesn't exist in the PrimeJobs system");
                    respond_acctEnable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " doesn't exist in the PrimeJobs system");
                }
                if (!userLocked(reqObj))
                {
                    Log("INFO", "User is already unlocked in the PrimeJobs system");
                    respond_acctEnable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " is already unlocked in the PrimeJobs system");
                }
                respond_acctEnable(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    this.m_sUsername = reqObj.m_accountName;
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_AcctEnable");
                    String query = "ALTER USER \"" + this.m_sUsername + "\" ACCOUNT UNLOCK";
                    executeCommand(query, null, "ADIBPrimeJobsCnctr_AcctEnable");
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBPrimeJobsCnctr_AcctEnable");
                    Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctEnable ===============");
                    respond_acctEnable(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }               
            }
        } // ADIBPrimeJobsCnctr_AcctEnable

        public void ADIBPrimeJobsCnctr_AcctDisable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBPrimeJobsCnctr_AcctDisable ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (!userExist(reqObj))
                {
                    Log("DEBUG", "User doesn't exist in the PrimeJobs system");
                    respond_acctDisable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " doesn't exist in the PrimeJobs system");
                }
                if (userLocked(reqObj))
                {
                    Log("DEBUG", "User is already locked in the PrimeJobs system");
                    respond_acctDisable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " is already locked in the PrimeJobs system");
                }
                respond_acctDisable(resObj, reqObj.m_accountName,this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    this.m_sUsername = reqObj.m_accountName;
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_AcctDisable");
                    String query = "ALTER USER \"" + this.m_sUsername + "\" ACCOUNT LOCK";
                    executeCommand(query, null, "ADIBPrimeJobsCnctr_AcctDisable");
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBPrimeJobsCnctr_AcctDisable");
                    Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctDisable ===============");
                    respond_acctDisable(resObj, this.m_sUsername, false, this.m_sUsername + " account disabled successfully");
                }

            }
        } // ADIBPrimeJobsCnctr_AcctDisable

        public void ADIBPrimeJobsCnctr_AcctDelete(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBPrimeJobsCnctr_AcctDelete ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (!userExist(reqObj))
                {
                    Log("DEBUG", "User doesn't exist in the PrimeJobs system");
                    respond_acctDelete(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " doesn't exist in the PrimeJobs system");
                }
                respond_acctDelete(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    this.m_sUsername = reqObj.m_accountName;

                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_AcctDelete");

                    Log(this.m_sUsername);

                    OracleCommand selectCmd = new OracleCommand("SELECT count(*) FROM dba_Users where Username='" + this.m_sUsername + "'", this.connection);
                    OracleDataReader reader = null;

                    reader = selectCmd.ExecuteReader();

                    while(reader.Read()){
                        Log("Count of User in PrimeJobs DB : "+reader["count(*)"].ToString());
                        if (Int16.Parse(reader["count(*)"].ToString()) == 0){
                            Log("User Doesn't Exists in PrimeJobs.");
                            throw new Exception("User Doesn't Exists in PrimeJobs.");
                        }
                    }

                    OracleCommand selectCmd_priv = new OracleCommand("select granted_role from dba_role_privs where grantee = '" + this.m_sUsername + "'", this.connection);
                    OracleDataReader reader_priv = null;

                    reader_priv = selectCmd_priv.ExecuteReader();

                    while (reader_priv.Read())
                    {
                        Log("Privilidge "+ reader_priv["granted_role"].ToString() + " assigned to User " + this.m_sUsername + ". Revoking.");
                        
                        executeCommand("revoke " + reader_priv["granted_role"].ToString() + " from " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctDelete");

                        Log("Privilidge " + reader_priv["granted_role"].ToString() + "Revoked.");
                    }

                    Log("Deleting User " + this.m_sUsername);
                    executeCommand("delete from primeusers where username='" + this.m_sUsername + "'", null, "ADIBPrimeJobsCnctr_AcctDelete");
                    Log("User " + this.m_sUsername + "Deleted Successfully.");

                    Log("Dropping User " + this.m_sUsername);
                    executeCommand("drop user " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctDelete");
                    Log("User " + this.m_sUsername + "Dropped Successfully.");
                    

                    /*
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBCSFCnctr_AcctDelete");
                    executeCommand("DELETE FROM inst_access WHERE user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctDelete");
                    executeCommand("DELETE FROM monitor_access WHERE user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctDelete");
                    executeCommand("DELETE FROM cca_access WHERE user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctDelete");
                    executeCommand("DELETE FROM user_name WHERE user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctDelete");
                    executeCommand("DELETE FROM swx.adib_user WHERE ch_user_id = \'" + this.m_sUsername + "\'", null, "ADIBCSFCnctr_AcctDelete");
                    executeCommand("DROP USER \"" + this.m_sUsername.ToUpper() + "\"", null, "ADIBCSFCnctr_AcctDelete");
                    */
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBPrimeJobsCnctr_AcctDelete");
                    Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctDelete ===============");
                    respond_acctDelete(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }

            }
        } // ADIBPrimeJobsCnctr_AcctDELETE

        public override void AssignSupportedScriptFunctions()
        {
            base.RedirectInterface(COUR_INTERFACE_VALIDATE_TARGET_CONFIG, ADIBPrimeJobsCnctr_ValidateTargetConfig, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CREATE, ADIBPrimeJobsCnctr_AcctCreate, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CHANGE, ADIBPrimeJobsCnctr_AcctChange, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_INFO, ADIBPrimeJobsCnctr_AcctInfo, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DISABLE, ADIBPrimeJobsCnctr_AcctDisable, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_ENABLE, ADIBPrimeJobsCnctr_AcctEnable, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DELETE, ADIBPrimeJobsCnctr_AcctDelete, true, true, false);
        }

    }
}
