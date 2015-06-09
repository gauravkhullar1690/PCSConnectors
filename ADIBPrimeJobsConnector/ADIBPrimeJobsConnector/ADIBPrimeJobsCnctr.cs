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

        private String prmjobsCentral;
        private String prmjobsEgypt;
        private String prmjobsUAE;

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
                attributesFile = !string.IsNullOrEmpty(config.AppSettings.Settings["AttributeXML"].Value) ? config.AppSettings.Settings["AttributeXML"].Value : "ADIBPrimeJobsAttributes.xml";
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
        private void makeConnection(String host, String port, String serviceName, String username, String password, String methodName)
        {
            Log("DEBUG", "Trying to establish connection to the PrimeJobs database for method " + methodName);
            Log("DEBUG", "retryCount = " + iRetryCount + " connect timout= " + iConnectionTimeout);
            //Log("DEBUG", "Parameters host : " + host + " port : "+port + " serviceName : "+serviceName + " username : "+username +" password : "+password);  
            this.connection = new OracleConnection();
            try
            {
                String connString = "user id=" + username + ";password=" + password + ";data source=" +
                    "(DESCRIPTION=(CONNECT_TIMEOUT=" + iConnectionTimeout + ")(RETRY_COUNT=" + iRetryCount + ")(ADDRESS=(PROTOCOL=tcp)" +
                    "(HOST=" + host + ")(PORT=" + port + "))(CONNECT_DATA=" +
                    "(SERVICE_NAME=" + serviceName + ")))";

                connection.ConnectionString = connString;

                connection.Open();
                Log("INFO", "Successfully connected to the PrimeJobs database for method " + methodName);
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
        private void executeCommand(String query, Dictionary<String, Object> parameters, String methodName)
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
            prmjobsCentral = reqObj.GetParameter("GROUPS-CENTRAL");
            prmjobsEgypt = reqObj.GetParameter("GROUPS-EGYPT");
            prmjobsUAE = reqObj.GetParameter("GROUPS-UAE");

            Log("DEBUG", "Attributes Initialized successfully for method " + methodName);
        }



        // This method converts the String into an Array Sorts the Values and then Joins them in UpperCase
        private String sortValues(String inputString, char seprator)
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


        // check weather the user for which the Action takes place exist or not
        private Boolean userExist(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user exist in the PrimeJobs system"); // TODO: Should never be added.
            // Initialize the username
            this.m_sUsername = reqObj.m_accountName;
            Log("DEBUG", "UserName: " + this.m_sUsername);
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "userExist");
            OracleCommand selectCmd = new OracleCommand("SELECT username, account_status FROM dba_users where username = '" + this.m_sUsername + "'", this.connection);
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

                    OracleCommand selectCmd = new OracleCommand("select dba_users.username,dba_users.Account_status,dba_users.expiry_date,dba_role_privs.granted_role from dba_users,dba_role_privs where dba_role_privs.grantee=dba_users.username and dba_users.Account_status NOT like 'EXPIRED%' and dba_users.username NOT IN ('ADIB','ADIBARCH','ADIBCEN','ADIBCENTER','ADIBEGY','ADIBPARAM','ADIBUAE','CRYSTAL','DDM','IBRO','IVRRO','MISPRIMERO','PRIMEWEB','SYS','SYSTEM','USSDRO','WEBAPPRO') and dba_users.username = '" + this.m_sUsername + "'", this.connection);
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
        }// ADIBPrimeJobsCnctr_AcctInfo

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
                    createUserPrimeJobs(listofRoles, this.m_sUsername);

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

        public void createUserPrimeJobs(String listofRoles, String userName)
        {
            OracleCommand selectQuery = new OracleCommand("SELECT count(*) from dba_users where username = '" + userName + "'", this.connection);
            OracleDataReader reader = selectQuery.ExecuteReader();
            while (reader.Read())
            {
                if (Int16.Parse(reader["count(*)"].ToString()) > 0)
                {
                    Log("User already exists in PrimeJobs.");
                }
                else
                {
                    Log("User " + userName + " Doesn't exists Creating.");
                    executeCommand("CREATE USER \"" + userName + "\" IDENTIFIED BY \"" + this.m_sPassword + "\"", null, "ADIBPrimeJobsCnctr_AcctCreate");
                    Log("User " + userName + "Created.");
                }
            }

            String[] arrayofRoles = listofRoles.Split(',');
            foreach (String role in arrayofRoles)
            {
                if (role.ToUpper().IndexOf("EODROLE") > 0)
                {
                    executeCommand("grant PRIME_EOD to " + userName, null, "ADIBPrimeJobsCnctr_AcctCreate");
                    executeCommand("grant PRIME_EOD_ROLE to " + userName, null, "ADIBPrimeJobsCnctr_AcctCreate");
                }
                Log("Assigning Role " + role + " to User " + userName);
                executeCommand("grant " + role + " to " + userName, null, "ADIBPrimeJobsCnctr_AcctCreate");
                Log("Assigned Role " + role + " to User " + userName);
            }

            executeCommand("grant connect to " + userName, null, "ADIBPrimeJobsCnctr_AcctCreate");
            Log("CONNECT Role Assigned to " + userName + ".");
            executeCommand("grant Prime_select to " + userName, null, "ADIBPrimeJobsCnctr_AcctCreate");
            Log("PRIME_SELECT Role Assigned " + userName + ".");
            executeCommand("grant prime_jobs to " + userName, null, "ADIBPrimeJobsCnctr_AcctCreate");
            Log("PRIME_JOBS Role Assigned " + userName + ".");

            Log("Creating User " + userName);
            executeCommand("Insert into primeusers( institution_id, serno, username, usertype, schemaname, logaction) Values (0, s_primeusers.nextval, '" + userName + "', 'U','ADIB','Create')", null, "ADIBPrimeJobsCnctr_AcctCreate");
            Log("Created User " + userName);
        }

        public void ADIBPrimeJobsCnctr_AcctChange(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                // Respond with not supported
                respond_statusNotSupported(resObj);
            }
            else
            {
                bool create = false;
                String username = "";
                String rolesList = "";
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
                            Log("DEBUG", "=============== In ADIBPrimeJobsCnctr_AcctUnlock ===============");
                        }
                        else
                        {
                            Log("DEBUG", "=============== In ADIBPrimeJobsCnctr_AcctReset ===============");
                        }
                    }
                    else
                    {
                        Log("DEBUG", "=============== In ADIBPrimeJobsCnctr_AcctChange ===============");
                    }

                    // Setup the target parameters
                    setupConfig(reqObj);

                    // Initialize the username
                    this.m_sUsername = reqObj.m_accountName;
                    Log("UserName : " + this.m_sUsername);

                    // making connection with the database
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_AcctChange");


                    if (reqObj.m_object == "Password Reset")
                    {
                        if (this.m_bUnlockOnly)
                        {
                            // Only Unlock the account. No password reset performed.
                            // Set not required parameters to default value, as they will be required to build the XML
                            this.m_bForceChangeAtNextLogon = false;
                            String query = "ALTER USER \"" + this.m_sUsername + "\" ACCOUNT UNLOCK";
                            executeCommand(query, null, "ADIBPrimeJobsCnctr_AcctUnlock");
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
                                executeCommand(query, null, "ADIBPrimeJobsCnctr_PasswordResetHelpDesk");
                                Log("INFO", "Password reset for user, " + this.m_sUsername + " successfully performed using Help Desk Reset.");
                            }
                            else
                            {
                                String query = "ALTER USER \"" + this.m_sUsername + "\" IDENTIFIED BY \"" + this.m_sPassword + "\"";
                                executeCommand(query, null, "ADIBPrimeJobsCnctr_PasswordResetSelfService");
                                Log("INFO", "Password reset for user, " + this.m_sUsername + " successfully performed using Self-Service Reset.");
                            }
                        }
                    }
                    else // Perform change action
                    {
                        try
                        {
                            setupConfig(reqObj);
                            Log("DEBUG", "=============== In ADIBPrimeJobsCnctr_AcctChange ===============");
                            // Setup the target parameters
                            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBPrimeJobsCnctr_AcctChange");
                            this.m_sUsername = reqObj.m_accountName;
                            Log("Request received to modify PrimeJobs User : " + reqObj.m_accountName);
                            initializeAttributes(reqObj, "ADIBPrimeJobsCnctr_AcctChange");

                            Log("Roles for Central : " + this.prmjobsCentral);
                            Log("Roles for Egypt : " + this.prmjobsEgypt);
                            Log("Roles for UAE : " + this.prmjobsUAE);
                            String listofRoles = "";
                            String preFix = "";

                            String ProfileUserID = this.m_sUsername.Substring(1, this.m_sUsername.Length - 1);
                            Log("Profile User ID : " + ProfileUserID);

                            String cUserID = "C" + ProfileUserID;
                            String eUserID = "E" + ProfileUserID;
                            String uUserID = "U" + ProfileUserID;

                            bool cUserExists = userExists(cUserID);
                            bool eUserExists = userExists(eUserID);
                            bool uUserExists = userExists(uUserID);

                            String[] cRolesAssigned = null;
                            String[] eRolesAssigned = null;
                            String[] uRolesAssigned = null;

                            if (cUserExists)
                            {
                                cRolesAssigned = rolesAssigned(cUserID);
                            }
                            if (eUserExists)
                            {
                                eRolesAssigned = rolesAssigned(eUserID);
                            }
                            if (uUserExists)
                            {
                                uRolesAssigned = rolesAssigned(uUserID);
                            }

                            String[] cNewRolesList = null;
                            String[] eNewRolesList = null;
                            String[] uNewRolesList = null;


                            if (this.prmjobsCentral != "")
                            {
                                cNewRolesList = this.prmjobsCentral.ToString().Split(',');
                            }
                            if (this.prmjobsEgypt != "")
                            {
                                eNewRolesList = this.prmjobsEgypt.ToString().Split(',');
                            }
                            if (this.prmjobsUAE != "")
                            {
                                uNewRolesList = this.prmjobsUAE.ToString().Split(',');
                            }

                            bool[] UsersCreate = { false, false, false }; //0 for Central, 1 for Egypt , 2 for UAE
                            bool[] UsersChange = { false, false, false }; //0 for Central, 1 for Egypt , 2 for UAE

                            try
                            {
                                Log("Central New Roles List : " + cNewRolesList.Length);
                                if (cNewRolesList.Length > 0)
                                {
                                    try
                                    {
                                        Log("Central Already Assigned Roles Length : " + cRolesAssigned.Length);
                                        if (cRolesAssigned.Length == 0)
                                        {
                                            UsersCreate[0] = true;
                                        }
                                        else
                                        {
                                            UsersChange[0] = userChangePossibility(cRolesAssigned, cNewRolesList, cUserID);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        UsersCreate[0] = true;
                                        UsersChange[0] = false;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log("NO New Roles for Central Location.");
                            }

                            try
                            {
                                Log("Egypt New Roles List : " + eNewRolesList.Length);
                                if (eNewRolesList.Length > 0)
                                {
                                    try
                                    {
                                        Log("Egypt Already Assigned Roles Length : " + eRolesAssigned.Length);
                                        if (eRolesAssigned.Length == 0)
                                        {
                                            UsersCreate[1] = true;
                                        }
                                        else
                                        {
                                            UsersChange[1] = userChangePossibility(eRolesAssigned, eNewRolesList, uUserID);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        UsersCreate[1] = true;
                                        UsersChange[1] = false;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log("NO New Roles for Egypt Location.");
                            }

                            try
                            {
                                Log("UAE New Roles List : " + uNewRolesList.Length);
                                if (uNewRolesList.Length > 0)
                                {
                                    try
                                    {
                                        Log("UAE Already Assigned Roles Length : " + uRolesAssigned.Length);
                                        if (uRolesAssigned.Length == 0)
                                        {
                                            UsersCreate[2] = true;
                                        }
                                        else
                                        {
                                            UsersChange[2] = userChangePossibility(uRolesAssigned, uNewRolesList, eUserID);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        UsersCreate[2] = true;
                                        UsersChange[2] = false;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log("NO New Roles for UAE Location.");
                            }

                            Log("User Change Array : " + UsersChange);
                            Log("User Create Array : " + UsersCreate);

                            int trueChangePos = Array.IndexOf(UsersChange, true);
                            int trueCreatePos = Array.IndexOf(UsersCreate, true);

                            if (trueChangePos > -1)
                            {
                                create = false;
                                if (trueChangePos == 0)
                                {
                                    preFix = "C";
                                    listofRoles = this.prmjobsCentral;
                                    rolesList = this.prmjobsCentral;
                                }
                                else if (trueChangePos == 1)
                                {
                                    preFix = "E";
                                    listofRoles = this.prmjobsEgypt;
                                    rolesList = this.prmjobsEgypt;
                                }
                                else if (trueChangePos == 2)
                                {
                                    preFix = "U";
                                    listofRoles = this.prmjobsUAE;
                                    rolesList = this.prmjobsUAE;
                                }
                            }

                            if (trueCreatePos > -1)
                            {
                                create = true;
                                if (trueCreatePos == 0)
                                {
                                    preFix = "C";
                                    listofRoles = this.prmjobsCentral;
                                    rolesList = this.prmjobsCentral;
                                }
                                else if (trueCreatePos == 1)
                                {
                                    preFix = "E";
                                    listofRoles = this.prmjobsEgypt;
                                    rolesList = this.prmjobsEgypt;
                                }
                                else if (trueCreatePos == 2)
                                {
                                    preFix = "U";
                                    listofRoles = this.prmjobsUAE;
                                    rolesList = this.prmjobsUAE;
                                }
                            }


                            String PrimeJobsUserID = preFix + ProfileUserID;
                            Log("PrimeJobs User ID : " + PrimeJobsUserID);
                            username = PrimeJobsUserID;

                            /*OracleCommand selectCmd_UserPrimeJobs = new OracleCommand("select USERNAME from primeusers where USERNAME = '" + PrimeJobsUserID + "'", this.connection);
                            OracleDataReader reader_UserPrimeJobs = null;

                            reader_UserPrimeJobs = selectCmd_UserPrimeJobs.ExecuteReader();

                            Log("User Exists in PrimeJobs : " + reader_UserPrimeJobs.HasRows);

                            if (reader_UserPrimeJobs.HasRows == false*/
                            if (create == true)
                            {
                                try
                                {
                                    username = PrimeJobsUserID;
                                    create = true;
                                    this.m_sPassword = reqObj.GetParameter("Password");
                                    Log("Password : " + this.m_sPassword);
                                    createUserPrimeJobs(listofRoles, PrimeJobsUserID);
                                }
                                catch (Exception ex)
                                {
                                    SetExceptionMessage(ex);
                                }
                                finally
                                {
                                    Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctChange ===============");
                                    closeConnection("ADIBPrimeJobsCnctr_AcctChange");
                                    respond_acctCreate(resObj, PrimeJobsUserID, this.bErr, this.sErrMsg);
                                }
                            }
                            else
                            {
                                try
                                {
                                    OracleCommand selectCmd_priv = new OracleCommand("select granted_role from dba_role_privs where grantee = '" + username + "'", this.connection);
                                    OracleDataReader reader_priv = null;

                                    reader_priv = selectCmd_priv.ExecuteReader();

                                    while (reader_priv.Read())
                                    {
                                        Log("Privilidge " + reader_priv["granted_role"].ToString() + " assigned to User " + username + ". Revoking.");

                                        executeCommand("revoke " + reader_priv["granted_role"].ToString() + " from " + username, null, "ADIBPrimeJobsCnctr_AcctDelete");

                                        Log("Privilidge " + reader_priv["granted_role"].ToString() + "Revoked.");
                                    }

                                    //String accountName = this.m_sUsername;
                                    String accountName = username;
                                    String rolesToAssign = "";
                                    rolesToAssign = rolesList;
                                    /*
                                    if (accountName.Substring(0, 1).ToUpper() == "U")
                                    {
                                        rolesToAssign = this.prmjobsUAE;
                                    }
                                    else if (accountName.Substring(0, 1).ToUpper() == "C")
                                    {
                                        rolesToAssign = this.prmjobsCentral;
                                    }
                                    else if (accountName.Substring(0, 1).ToUpper() == "E")
                                    {
                                        rolesToAssign = this.prmjobsEgypt;
                                    }
                                    */
                                    Log("Roles To Assign : " + rolesToAssign);

                                    String[] arrayofRoles = rolesToAssign.Split(',');
                                    foreach (String role in arrayofRoles)
                                    {
                                        if (role.ToUpper().IndexOf("EODROLE") > 0)
                                        {
                                            executeCommand("grant PRIME_EOD to " + username, null, "ADIBPrimeJobsCnctr_AcctChange");
                                            executeCommand("grant PRIME_EOD_ROLE to " + username, null, "ADIBPrimeJobsCnctr_AcctChange");
                                        }
                                        Log("Assigning Role " + role + " to User " + username);
                                        executeCommand("grant " + role + " to " + username, null, "ADIBPrimeJobsCnctr_AcctChange");
                                        Log("Assigned Role " + role + " to User " + username);
                                    }

                                    executeCommand("grant connect to " + username, null, "ADIBPrimeJobsCnctr_AcctChange");
                                    Log("Assigned Role connect to User " + username);
                                    executeCommand("grant Prime_select to " + username, null, "ADIBPrimeJobsCnctr_AcctChange");
                                    Log("Assigned Role Prime_select to User " + username);
                                    executeCommand("grant prime_jobs to " + username, null, "ADIBPrimeJobsCnctr_AcctChange");
                                    Log("Assigned Role prime_jobs to User " + username);
                                }
                                catch (Exception ex)
                                {
                                    SetExceptionMessage(ex);
                                }
                                finally
                                {
                                    Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctChange ===============");
                                    closeConnection("ADIBPrimeJobsCnctr_AcctChange");
                                    respond_acctChange(resObj, username, this.bErr, this.sErrMsg);
                                }
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                        finally
                        {

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
                            Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctUnlock ===============");
                            closeConnection("ADIBPrimeJobsCnctr_AcctUnlock");
                        }
                        else
                        {
                            Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctReset ===============");
                            closeConnection("ADIBPrimeJobsCnctr_AcctReset");
                        }
                        respond_acctChange(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                    }
                    else if (create == true)
                    {
                        Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctChange ===============");
                        closeConnection("ADIBPrimeJobsCnctr_AcctChange");
                        respond_acctCreate(resObj, username, this.bErr, this.sErrMsg);
                    }
                    else
                    {
                        Log("DEBUG", "=============== Out ADIBPrimeJobsCnctr_AcctChange ===============");
                        closeConnection("ADIBPrimeJobsCnctr_AcctChange");
                        respond_acctChange(resObj, username, this.bErr, this.sErrMsg);
                    }

                }
            }
        } // ADIBPrimeJobsCnctr_AcctChange

        public bool userChangePossibility(String[] ExistingRolesList, String[] NewRolesList, String userID)
        {
            foreach (String str in NewRolesList)
            {
                Log("Index Of : " + Array.IndexOf(ExistingRolesList, str));
                if (Array.IndexOf(ExistingRolesList, str) == -1)
                {
                    Log("Change Possibility for " + userID + " : true.");
                    return true;
                }
            }

            Log("Change Possibility for " + userID + " : false.");
            return false;
        }

        public bool userExists(String userID)
        {
            OracleCommand selectCmd_UserPrimeJobs = new OracleCommand("select USERNAME from primeusers where USERNAME = '" + userID + "'", this.connection);
            OracleDataReader reader_UserPrimeJobs = null;

            reader_UserPrimeJobs = selectCmd_UserPrimeJobs.ExecuteReader();

            Log("User Exists in PrimeJobs with UserID " + userID + " : " + reader_UserPrimeJobs.HasRows);

            if (reader_UserPrimeJobs.HasRows == false)
            {
                return false;
            }
            return true;
        }

        public String[] rolesAssigned(String sUserId)
        {
            OracleCommand selectCmd_priv = new OracleCommand("select granted_role from dba_role_privs where grantee = '" + sUserId + "'", this.connection);
            OracleDataReader reader_priv = null;

            reader_priv = selectCmd_priv.ExecuteReader();
            List<String> rolesList = new List<String>();

            while (reader_priv.Read())
            {
                rolesList.Add(reader_priv["granted_role"].ToString());
            }

            Log("Roles already assigned to User " + sUserId + " are : " + rolesList.ToArray());

            return rolesList.ToArray();
        }

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
                respond_acctDisable(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
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

                    while (reader.Read())
                    {
                        Log("Count of User in PrimeJobs DB : " + reader["count(*)"].ToString());
                        if (Int16.Parse(reader["count(*)"].ToString()) == 0)
                        {
                            Log("User Doesn't Exists in PrimeJobs.");
                            throw new Exception("User Doesn't Exists in PrimeJobs.");
                        }
                    }

                    OracleCommand selectCmd_priv = new OracleCommand("select granted_role from dba_role_privs where grantee = '" + this.m_sUsername + "'", this.connection);
                    OracleDataReader reader_priv = null;

                    reader_priv = selectCmd_priv.ExecuteReader();

                    while (reader_priv.Read())
                    {
                        Log("Privilidge " + reader_priv["granted_role"].ToString() + " assigned to User " + this.m_sUsername + ". Revoking.");

                        executeCommand("revoke " + reader_priv["granted_role"].ToString() + " from " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctDelete");

                        Log("Privilidge " + reader_priv["granted_role"].ToString() + "Revoked.");
                    }

                    Log("Deleting User " + this.m_sUsername);
                    executeCommand("delete from primeusers where username='" + this.m_sUsername + "'", null, "ADIBPrimeJobsCnctr_AcctDelete");
                    Log("User " + this.m_sUsername + "Deleted Successfully.");

                    Log("Dropping User " + this.m_sUsername);
                    executeCommand("drop user " + this.m_sUsername, null, "ADIBPrimeJobsCnctr_AcctDelete");
                    Log("User " + this.m_sUsername + "Dropped Successfully.");

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
