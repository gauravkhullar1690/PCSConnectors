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
// Author: ROHIT PANT
// Date: 30 April 2015
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
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Xml;

namespace ADIBWorkstreamConnector
{
    public class ADIBWorkstreamCnctr : RDKCore
    {
        // Target Validation Parameters
        private string m_sHost;
        private string m_sPort;
        private string m_sDatabaseName;
        private string m_sDBUserName;
        private string m_sDBPassword;

        // connection objects
        private SqlConnection connection;
        private SqlCommand command;
        
        // Define the Log object and required variables
        private COURPROFILERLib.CourProfileLog2Class _log_obj = null;
        private string uid;
        private string _log_file;
        private bool _log_level = true;
        private int iConnectionTimeout = 15;
        private int iRetryCount = 0;

        private string m_sRequestXMLData;

        // Supporting variables
        private bool bErr = false;
        private string sErrMsg;
        private string sResult;

        // Operational Parameters
        private string m_sUsername;
        private string m_sPassword;
        private string m_sEmail;
        private string m_sValidTo;
        private string m_sInternalId;
        private string m_sFirstName;
        private string m_sLastName;
        private string m_sAccountClosureGps;
        private const char multiValueSeprator = ',';

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

        public ADIBWorkstreamCnctr()
            : base("CourionWorkstreamCnctr")
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
                attributesFile = !string.IsNullOrEmpty(config.AppSettings.Settings["AttributeXML"].Value) ? config.AppSettings.Settings["AttributeXML"].Value : "ADIBWorkstreamAttributes.xml";
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
        private SqlConnection makeConnection(String host, String port, String databaseName, String username, String password, String methodName)
        {
            Log("DEBUG", "Trying to establish connection to the CSF database for method " + methodName);
            Log("DEBUG", "retryCount = " + iRetryCount + " connect timout= " + iConnectionTimeout);
            //Log("DEBUG", "Parameters host : " + host + " port : "+port + " serviceName : "+serviceName + " username : "+username +" password : "+password);  
            this.connection = new SqlConnection();
            try
            {
                String connString = "Data Source="+host+","+port+";Initial Catalog="+databaseName+";User ID="+username+";Password="+password;

                connection.ConnectionString = connString;

                connection.Open();
                Log("INFO", "Successfully connected to the CSF database for method " + methodName);
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to CSF database for method " + methodName + " :: " + e.Message);
                throw e;
            }
            return this.connection;
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
                Log("ERROR", "Error while connecting to CSF database :: " + e.Message);
                throw e;
            }

        }

        private List<List<object>> getResultsFromSqlDb(SqlConnection conn, string query)
        {
            List<List<object>> resultSet = new List<List<object>>();
            Log("INFO", "Select Query to be executed : " + query);
            try
            {
                SqlCommand command = new SqlCommand(query, conn);
                SqlDataReader reader = command.ExecuteReader();
                List<object> rowSet = null;
                int colCounter;
                while (reader.Read())
                {
                    colCounter = 0;
                    rowSet = new List<object>();
                    while (colCounter < reader.FieldCount)
                    {
                        rowSet.Add(reader.GetValue(colCounter));
                        colCounter++;
                    }
                    resultSet.Add(rowSet);
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Exception occurred --- " + e.Message);
            }
            string resultStr = "";
            foreach(List<object> row in resultSet)
            {
                if (resultStr.Length > 0)
                {
                    resultStr += "\n";
                }
                foreach (object cell in row)
                {
                    if (resultStr.Length > 0)
                    {
                        resultStr += "," + cell;
                    }
                    else
                    {
                        resultStr += cell;
                    }
                }
            }
            Log("INFO", "Result for the Query is : " + resultStr);
            return resultSet;
        }

        private int setResultsToSqlDb(SqlConnection conn, string query)
        {
            int rowsEffected = -1;
            Log("INFO", "Query to be executed : " + query);
            try
            {
                SqlCommand command = conn.CreateCommand();
                command.CommandText = query;
                rowsEffected = command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Log("ERROR", "Exception occurred --- " + e.Message);
            }
            Log("INFO", "Number of rows effected by query are : " + rowsEffected);
            return rowsEffected;
        }

        // initialize the attributes
        private void initializeAttributes(RequestObject reqObj, String methodName)
        {
            Log("DEBUG", "Initializing the attributes for method " + methodName);
            this.m_sUsername = reqObj.m_accountName;
            this.m_sPassword = reqObj.GetParameter("Password");
            this.m_sValidTo = reqObj.GetParameter("ValidTo");
            this.m_sInternalId = reqObj.GetParameter("InternalId");
            this.m_sEmail = reqObj.GetParameter("Email");
            this.m_sFirstName = reqObj.GetParameter("FirstName");
            this.m_sLastName = reqObj.GetParameter("LastName");
            this.m_sAccountClosureGps = reqObj.GetParameter("AccountClosureGroups");
            Log("DEBUG", "Attributes Initialized successfully for method " + methodName);
        }

        // check weather the user for which the Action takes place exist or not
        private Boolean userExist(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user exist in the Workstream system"); // TODO: Should never be added.
            // Initialize the username
            initializeAttributes(reqObj, "userExist");
            Log("DEBUG", "UserName: " + reqObj.m_accountName);
            SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "userExist");
            List<List<object>> result = getResultsFromSqlDb(conn, "SELECT firstname, lastname, email FROM t_user where name = '" + reqObj.m_accountName + "'");
            if (result.Count > 0)
            {
                closeConnection("userExist");
                return true;
            }
            closeConnection("userExist");
            return false;
        }

        private Boolean isUserActive(RequestObject reqObj)
        {
            if (userExist(reqObj))
            {
                Log("DEBUG", "Checking if the user is active in the Workstream system"); // TODO: Should never be added.
                SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "isUserActive");
                List<List<object>> result = getResultsFromSqlDb(conn, "select activate from t_user where name = '" + this.m_sUsername + "'");
                foreach (List<object> row in result)
                {
                    foreach (object cell in row)
                    {
                        if (cell.ToString().Equals("1"))
                        {
                            Log("INFO", "User - " + this.m_sUsername + " is Active in Workstream system");
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            else
            {
                Log("INFO", "User - " + this.m_sUsername + " is not present in Workstream system");
            }
            return false;
        }

        private string getIdFromTable(string username, string tablename)
        {
            string id = "";
            Log("DEBUG", "Getting ID from Username");
            try
            {
                string query = "select id from " + tablename + " where name = '" + username + "'";
                SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "getIdFromTable");
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                Boolean found = false;
                foreach (List<object> row in results)
                {
                    foreach (object cell in row)
                    {
                        id = cell.ToString();
                        found = true;
                        break;
                    }
                    if (found)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Exception occurred : " + e.Message);
            }
            return id;
        }

        // returns the list of access details the User has
        private StrList getAccessDetailsList(String userId)
        {
            Log("DEBUG", "Getting access details list");
            StrList accessDtls = new StrList();
            try
            {
                string query = "select domain_name,name from t_role where id in (select ur.role_id from t_user t join t_user_role ur on t.id = ur.user_id where t.name = '"+userId+"')";
                SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBWorkstreamCnctr_ValidateTargetConfig");
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                Log("INFO", "Access details query :: " + query);
                string rowStr;
                foreach (List<object> rows in results)
                {
                    rowStr = "";
                    foreach (object cell in rows)
                    {
                        if (rowStr.Length > 0)
                        {
                            rowStr += " - " + cell;
                        }
                        else
                        {
                            rowStr += cell;
                        }
                    }
                    accessDtls.Add(rowStr);
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while fetching access details for user " + userId + " Error Message " + e.Message);
                throw new Exception("Failed to fetch Access details for User Please Contact System Administrator");
            }
            Log("DEBUG", "Returning access details list " + accessDtls.Count);
            return accessDtls;
        }

        // get User Group List
        private StrList getUserGroupsList(String userId)
        {
            Log("DEBUG", "Getting User group list");
            StrList grpList = new StrList();
            try
            {
                string query = "select name from t_role where id in (select ur.role_id from t_user t join t_user_role ur on t.id = ur.user_id where t.name = '"+userId+"')";
                SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "getUserGroupsList");
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                Log("INFO", "Groups details query :: " + query);
                string rowStr;
                foreach (List<object> rows in results)
                {
                    rowStr = "";
                    foreach (object cell in rows)
                    {
                        rowStr += cell;
                    }
                    grpList.Add(rowStr);
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

        public void ADIBWorkstreamCnctr_ValidateTargetConfig(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBWorkstreamCnctr_ValidateTargetConfig ===============");
            try
            {
                // Setup the target parameters
                setupConfig(reqObj);
                Log("INFO", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBWorkstreamCnctr_ValidateTargetConfig");
                    Log("INFO", "Target validated successfully.");
                    closeConnection("ADIBWorkstreamCnctr_ValidateTargetConfig");
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
                Log("INFO", "=============== Out ADIBWorkstreamCnctr_ValidateTargetConfig ===============");
                respond_validateTargetConfiguration(resObj, this.bErr, this.sErrMsg);
            }
        } // ADIBCSFCnctr_ValidateTargetConfig

        public void ADIBWorkstreamCnctr_EnableUser(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBWorkstreamCnctr_EnableUser ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                try
                {
                    if (!userExist(reqObj))
                    {
                        Log("INFO", "User doesn't exist in the Workstream system");
                        throw new Exception("User doesn't exist in the Workstream system");
                    }
                    if (isUserActive(reqObj))
                    {
                        Log("INFO", "User is already unlocked in the Workstream system");
                        throw new Exception("User is already unlocked in the Workstream system");
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    Log("DEBUG", "=============== Out ADIBWorkstreamCnctr_EnableUser ===============");
                    respond_acctEnable(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    this.m_sUsername = reqObj.m_accountName;
                    SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBWorkstreamCnctr_EnableUser");
                    String query = "update t_user set activate = 1 where name = '"+this.m_sUsername+"'";
                    setResultsToSqlDb(conn, query);
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBWorkstreamCnctr_EnableUser");
                    Log("DEBUG", "=============== Out ADIBWorkstreamCnctr_EnableUser ===============");
                    respond_acctEnable(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBWorkstreamCnctr_EnableUser

        public void ADIBWorkstreamCnctr_DisableUser(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBWorkstreamCnctr_DisableUser ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                try
                {
                    if (!userExist(reqObj))
                    {
                        Log("INFO", "User doesn't exist in the Workstream system");
                        throw new Exception("User doesn't exist in the Workstream system");
                    }
                    if (!isUserActive(reqObj))
                    {
                        Log("INFO", "User is already locked in the Workstream system");
                        throw new Exception("User doesn't exist in the Workstream system");
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    Log("DEBUG", "=============== Out ADIBWorkstreamCnctr_DisableUser ===============");
                    respond_acctDisable(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    this.m_sUsername = reqObj.m_accountName;
                    SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBWorkstreamCnctr_DisableUser");
                    String query = "update t_user set activate = 0 where name = '" + this.m_sUsername + "'";
                    setResultsToSqlDb(conn, query);
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBWorkstreamCnctr_DisableUser");
                    Log("DEBUG", "=============== Out ADIBWorkstreamCnctr_DisableUser ===============");
                    respond_acctDisable(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBWorkstreamCnctr_DisableUser

        public void ADIBWorkstreamCnctr_AcctInfo(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                    Log("INFO", "=============== In ADIBWorkstreamCnctr_AcctInfo ===============");
                    // Setup the target parameters
                    setupConfig(reqObj);
                    Log("DEBUG", "Request XML from CCM: " + reqObj.xmlDoc); // TODO: Should never be added.
                    // Initialize the username
                    Log("DEBUG", "UserName: " + reqObj.m_accountName);

                    makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBWorkstreamCnctr_AcctInfo");
                    // adding basic details

                    // initilizing the attributes
                    initializeAttributes(reqObj, "ADIBWorkstreamCnctr_AcctInfo");

                    // adding access details for User to show
                    mapAttrsToValues.SetParamValues("AccessDetails", getAccessDetailsList(reqObj.m_accountName));

                    // adding User group list
                    mapAttrsToValues.SetParamValues("AccountClosureGroups", getUserGroupsList(reqObj.m_accountName));

                    Log("Account: " + reqObj.m_accountName + " fetched successfully.");
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                }
                finally
                {
                    Log("=============== Out ADIBWorkstreamCnctr_AcctInfo ===============");
                    respond_acctInfo(resObj, mapAttrsToValues, lstNotAllowed, this.bErr, this.sErrMsg);
                }
            }
        }// ADIBWorkstreamCnctr_AcctInfo


        public void ADIBWorkstreamCnctr_AcctCreate(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBWorkstreamCnctr_AcctCreate ===============");
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
                    SqlConnection conn = makeConnection(this.m_sHost, this.m_sPort, this.m_sDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBWorkstreamCnctr_AcctCreate");
                    Log("Request received to create AMS User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBWorkstreamCnctr_AcctCreate");
                    if (!userExist(reqObj))
                    {
                        string query = "insert into t_user (activate,disable,email,expireevery,firstName,lastName,name,privy) values(1,0,'" + this.m_sEmail + "',90,'" + this.m_sFirstName + "','" + this.m_sLastName + "','" + this.m_sUsername + "',2)";
                        if (this.m_sAccountClosureGps.Equals(""))
                        {
                            throw new Exception("Atleast 1 role needs to be provisioned");
                        }
                        int rowsEffected = setResultsToSqlDb(conn, query);
                        if (rowsEffected > 0)
                        {
                            Log("DEBUG", "User successfully created in t_user table");
                            string userid = getIdFromTable(this.m_sUsername, "t_user"), roleid;
                            String[] groups = this.m_sAccountClosureGps.Split(multiValueSeprator);
                            if (groups.Length == 0)
                            {
                                groups = new String[] { this.m_sAccountClosureGps };
                            }
                            foreach (String group in groups)
                            {
                                roleid = getIdFromTable(group, "t_role");
                                query = "insert into t_user_role values(" + userid + "," + roleid + ")";
                                setResultsToSqlDb(conn, query);
                            }
                            Log("DEBUG", "User successfully provisioned!!");
                        }
                        else
                        {
                            Log("DEBUG", "Unable to create user in t_user table");
                        }
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBWorkstreamCnctr_AcctCreate");
                    Log("DEBUG", "=============== Out ADIBWorkstreamCnctr_AcctCreate ===============");
                    respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }

            }
        } // ADIBAMSCnctr_AcctCreate

        public override void AssignSupportedScriptFunctions()
        {
            base.RedirectInterface(COUR_INTERFACE_VALIDATE_TARGET_CONFIG, ADIBWorkstreamCnctr_ValidateTargetConfig, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_INFO, ADIBWorkstreamCnctr_AcctInfo, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_ENABLE, ADIBWorkstreamCnctr_EnableUser, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DISABLE, ADIBWorkstreamCnctr_DisableUser, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CREATE, ADIBWorkstreamCnctr_AcctCreate, false, true, false);
        }
    }
}
