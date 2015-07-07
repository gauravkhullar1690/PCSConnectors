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
// Date: 01 April 2015
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
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;

namespace ADIBAmbitCnctr
{
    public class ADIBAmbitCnctr : RDKCore
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
        private string m_sEmployeeNo;
        private string m_sFirstName;
        private string m_sMiddleName;
        private string m_sLastName;
        private string m_sFullName;
        private string m_sGender;
        private string m_sBranchCode;
        private string m_sDepartmentCode;
        private string m_sUserType;
        private string m_sLimitType;
        private string m_sRoles;
        private string m_sAccessControlLimit;
        private string m_sAccessCode;

        // Supporting variables
        private const String AmbitStoredProcName = "employee_user_maintenance";
        private bool bErr = false;
        private string sErrMsg;

        // Charater used seprate multiple values
        private const char multiValueSeprator = ',';

        // Define the Log object and required variables
        private COURPROFILERLib.CourProfileLog2Class _log_obj = null;
        //private string uid;
        private string _log_file;
        private bool _log_level = true;
        private int iConnectionTimeout = 15;
        private int iRetryCount = 0;

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



        public ADIBAmbitCnctr()
            : base("CourionAmbitCnctr")
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
                attributesFile = !string.IsNullOrEmpty(config.AppSettings.Settings["AttributeXML"].Value) ? config.AppSettings.Settings["AttributeXML"].Value : "ADIBAmbitAttributes.xml";
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
            Log("DEBUG", "Trying to establish connection to the Ambit database for method " + methodName);
            Log("DEBUG", "retryCount = " + iRetryCount + " connect timout= " + iConnectionTimeout);
            //Log("DEBUG", "Parameters host : " + host + " port : "+port + " serviceName : "+serviceName + " username : "+username +" password : "+password);  
            this.connection = new OracleConnection();
            try
            {
                String connString = "user id=" + username + ";password=" + password + ";data source=" +
                    "(DESCRIPTION=(CONNECT_TIMEOUT=" + iConnectionTimeout + ")(RETRY_COUNT=" + iRetryCount + ")(ADDRESS=(PROTOCOL=tcp)" +
                    "(HOST=" + host + ")(PORT=" + port + "))(CONNECT_DATA=" +
                    "(SERVICE_NAME=" + serviceName + ")))";

                this.connection.ConnectionString = connString;

                this.connection.Open();
                Log("INFO", "Successfully connected to the Ambit database for method " + methodName);
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to Ambit database for method " + methodName + " :: " + e.Message);
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
                Log("ERROR", "Error while connecting to Ambit database :: " + e.Message);
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

        // Execute Reader
        private StrList executeReader(String query, List<String> columns, String methodName)
        {
            StrList list = new StrList();
            try
            {
                OracleCommand command = new OracleCommand(query, this.connection);
                OracleDataReader reader = null;
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

        // code to execute the Stored Proceddure
        private void executeStoredProc()
        {
            Log("DEBUG", "Inside method executeStoredProc");
            try
            {
                this.command.ExecuteNonQuery();
                Log("DEBUG", "Stored procedure executed without exceptions");
            }
            catch (Exception e)
            {
                Log("DEBUG", "Exception occurred while executing stored procedure : " + e.Message);
                throw e;
            }
            Log("DEBUG", "Exiting method executeStoredProc");
        }

        private void constructSPParameters(String p_action, String p_empid, String p_firstname, String p_middlename, String p_lastname, String p_gender, String p_fullname, string p_branchcode, string p_departmentcode, string p_usertype, string p_limittype, string p_roleIds, string p_aclIds, string p_accesscode)
        {
            Log("DEBUG", "Inside method constructSPParameters");
            addInBuiltInputParameter("p_action", OracleDbType.Varchar2, p_action);
            addInBuiltInputParameter("p_empid", OracleDbType.Varchar2, p_empid);
            addInBuiltInputParameter("p_firstname", OracleDbType.Varchar2, p_firstname);
            addInBuiltInputParameter("p_middlename", OracleDbType.Varchar2, p_middlename);
            addInBuiltInputParameter("p_lastname", OracleDbType.Varchar2, p_lastname);
            addInBuiltInputParameter("p_gender", OracleDbType.Varchar2, p_gender);
            addInBuiltInputParameter("p_fullname", OracleDbType.Varchar2, p_fullname);
            addInBuiltInputParameter("p_branchcode", OracleDbType.Varchar2, p_branchcode);
            addInBuiltInputParameter("p_departmentcode", OracleDbType.Varchar2, p_departmentcode);
            addInBuiltInputParameter("p_usertype", OracleDbType.Varchar2, p_usertype);
            addInBuiltInputParameter("p_limittype", OracleDbType.Varchar2, p_limittype);
            addInBuiltInputParameter("p_roleIds", OracleDbType.Varchar2, p_roleIds);
            addInBuiltInputParameter("p_aclIds", OracleDbType.Varchar2, p_aclIds);
            addInBuiltInputParameter("p_accesscode", OracleDbType.Varchar2, p_accesscode);
            addInBuiltOutputParameter("p_returncode", OracleDbType.Varchar2, 1);
            addInBuiltOutputParameter("p_err_num", OracleDbType.Int32, -1);
            addInBuiltOutputParameter("p_err_msg", OracleDbType.Varchar2, 100);
            Log("DEBUG", "Exiting method constructSPParameters");
        }

        // returns the Id from the label.
        private String[] getIdsFromLabels(String tableName, String iDFieldName, String labelFieldName, String labels, String category)
        {
            List<String> returnList = new List<String>();
            try
            {
                String inClause = formatLabel(labels);
                OracleCommand selectQuery = new OracleCommand("SELECT " + iDFieldName + " from " + tableName + " where " + labelFieldName + " in (" + inClause + " )", this.connection);
                Log("INFO", "QUERY :: SELECT " + iDFieldName + " from " + tableName + " where " + labelFieldName + " in (" + inClause + " )");
                if (category.ToUpper().Equals("ROLE"))
                {
                    returnList.Add("ROLE1");
                }
                OracleDataReader reader = selectQuery.ExecuteReader();
                while (reader.Read())
                {
                    returnList.Add(reader[0].ToString());
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            return returnList.ToArray();
        }

        // takes the comman seprated array string and appends ' on both sides of item and returns the String
        private String formatLabel(String arrString)
        {
            String[] arr = arrString.Split(multiValueSeprator);
            List<String> list = new List<String>();

            foreach (String label in arr)
            {
                list.Add("'" + label + "'");
            }
            return String.Join(multiValueSeprator.ToString(), list.ToArray());
        }

        private string getFullName()
        {
            string fullName = "";
            if (this.m_sFirstName != null && this.m_sFirstName != "")
            {
                fullName += this.m_sFirstName;
            }
            if (this.m_sMiddleName != null && this.m_sMiddleName != "")
            {
                if (fullName != "")
                {
                    fullName += " " + this.m_sMiddleName;
                }
                else
                {
                    fullName += this.m_sMiddleName;
                }
            }
            if (this.m_sLastName != null && this.m_sLastName != "")
            {
                if (fullName != "")
                {
                    fullName += " " + this.m_sLastName;
                }
                else
                {
                    fullName += this.m_sLastName;
                }
            }
            return fullName;
        }

        // initialize the attributes
        private void initializeAttributes(RequestObject reqObj, String methodName)
        {
            Log("DEBUG", "Initializing the attributes for method " + methodName);
            this.m_sEmployeeNo = reqObj.m_accountName;
            this.m_sFirstName = reqObj.GetParameter("FirstName");
            this.m_sMiddleName = reqObj.GetParameter("MiddleName");
            this.m_sLastName = reqObj.GetParameter("LastName");
            this.m_sGender = reqObj.GetParameter("Gender");
            this.m_sBranchCode = reqObj.GetParameter("BranchCode");
            this.m_sDepartmentCode = reqObj.GetParameter("DepartmentCode");
            this.m_sUserType = reqObj.GetParameter("UserType");
            this.m_sLimitType = reqObj.GetParameter("LimitType");
            this.m_sRoles = reqObj.GetParameter("Roles");
            this.m_sAccessControlLimit = reqObj.GetParameter("AccessControlLimit");
            this.m_sAccessCode = reqObj.GetParameter("AccessCode");
            this.m_sFullName = getFullName();
            Log("DEBUG", "Attributes Initialized successfully for method " + methodName);
        }

        private string getAttributeValue(RequestObject reqObj, string attributeName)
        {
            string value = null;
            setupConfig(reqObj);
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "getAttributeValue");
            OracleCommand selectCmd = new OracleCommand("select BRANCHCODE, USERTYPE, LIMITTYPE from ns_empworkinfo where empid = '" + reqObj.m_accountName + "'", this.connection);
            OracleDataReader reader = null;
            reader = selectCmd.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    value = reader[attributeName].ToString();
                    reader.Close();
                    closeConnection("getAttributeValue");
                    return value;
                }
            }
            reader.Close();
            closeConnection("getAttributeValue");
            return value;
        }

        // check weather the user for which the Action takes place exist or not
        private Boolean userExist(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user exist in the Ambit system");
            // Initialize the username            
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "userExist");
            OracleCommand selectCmd = new OracleCommand("select EMPID from ns_empbasicinfo where empid = '" + reqObj.m_accountName + "'", this.connection);
            OracleDataReader reader = null;
            reader = selectCmd.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    if (reader["EMPID"].Equals(reqObj.m_accountName))
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
            Log("DEBUG", "Checking if the user is locked in the Ambit system");
            // Initialize the username
            this.m_sEmployeeNo = reqObj.m_accountName;
            Log("DEBUG", "UserName: " + this.m_sEmployeeNo);
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "userLocked");
            OracleCommand selectCmd = new OracleCommand("select STATUS from ns_userchannelinfo where userid = '" + this.m_sEmployeeNo + "'", this.connection);
            OracleDataReader reader = null;
            reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader["STATUS"].Equals("DISABLED"))
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

        // create the Stored Procedure Command
        private void createExecStoredProcedure(String spName)
        {
            Log("DEBUG", "Inside method createExecStoredProcedure. Stored Procedure - " + spName + " will be executed");
            this.command = this.connection.CreateCommand();
            this.command.CommandText = spName;
            this.command.CommandType = CommandType.StoredProcedure;
            Log("DEBUG", "Exiting method createExecStoredProcedure");
        }

        private void addInBuiltInputParameter(String parameterName, OracleDbType dataType, String value)
        {
            Log("DEBUG", "Inside method addInBuiltInputParameter");
            if (value != null && value != "")
            {
                OracleParameter parameter = new OracleParameter();
                parameter.ParameterName = parameterName;
                parameter.OracleDbType = dataType;
                parameter.Direction = ParameterDirection.Input;
                parameter.Value = value;
                this.command.Parameters.Add(parameter);
                Log("DEBUG", "Parameter : " + parameterName + " successfully added");
                Log("INFO", parameter.ParameterName + " :: " + parameter.Value);
                parameter.Dispose();
            }
            else
            {
                OracleParameter parameter = new OracleParameter();
                parameter.ParameterName = parameterName;
                parameter.OracleDbType = dataType;
                parameter.Direction = ParameterDirection.Input;
                parameter.Value = "";
                this.command.Parameters.Add(parameter);
                Log("DEBUG", "Parameter : " + parameterName + " successfully added");
                Log("INFO", parameter.ParameterName + " :: " + parameter.Value);
                parameter.Dispose();
            }
            Log("DEBUG", "Exiting method addInBuiltInputParameter");
        }

        private void addInBuiltOutputParameter(String parameterName, OracleDbType dataType, int size)
        {
            Log("DEBUG", "Inside method addInBuiltOutputParameter");
            OracleParameter parameter = new OracleParameter();
            parameter.ParameterName = parameterName;
            parameter.OracleDbType = dataType;
            if (size > 0)
            {
                parameter.Size = size;
            }
            parameter.Direction = ParameterDirection.Output;
            this.command.Parameters.Add(parameter);
            Log("DEBUG", "Parameter : " + parameterName + " successfully added");
            Log("DEBUG", "Exiting method addInBuiltOutputParameter");
        }

        private void canHaveOnePossibleInput(string attributeValue, string exceptionMsg)
        {
            Log("DEBUG", "Inside method canHaveOnePossibleInput");
            if (attributeValue.Split(multiValueSeprator).Length > 1)
            {
                Log("DEBUG", "Contains more the 1 inputs, will throw an exception : " + exceptionMsg);
                throw new Exception(exceptionMsg);
            }
        }

        private void addUDTArrayParameter(String parameterName, String UdtTypeName, OracleDbType dataType, ParameterDirection direction, String[] value)
        {
            VARRAYTEST customParameter = new VARRAYTEST();
            customParameter.Array = value;
            OracleParameter parameter = new OracleParameter();
            parameter.ParameterName = parameterName;
            parameter.OracleDbType = dataType;
            parameter.Direction = direction;
            parameter.UdtTypeName = UdtTypeName;
            parameter.Value = customParameter;
            this.command.Parameters.Add(parameter);
            Log("INFO", parameter.ParameterName + " :: " + parameter.Value);
            if (direction == ParameterDirection.Input)
            {
                parameter.Dispose();
            }
        }

        public void ADIBAmbitCnctr_ValidateTargetConfig(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAmbitCnctr_ValidateTargetConfig ===============");
            try
            {
                // Setup the target parameters
                setupConfig(reqObj);
                Log("INFO", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAmbitCnctr_ValidateTargetConfig");
                    Log("INFO", "Target validated successfully.");
                    closeConnection("ADIBAmbitCnctr_ValidateTargetConfig");
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
                Log("INFO", "=============== Out ADIBAmbitCnctr_ValidateTargetConfig ===============");
                respond_validateTargetConfiguration(resObj, this.bErr, this.sErrMsg);
            }
        } // ADIBAmbitCnctr_ValidateTargetConfig

        public void ADIBAmbitCnctr_AcctInfo(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                    Log("INFO", "=============== In ADIBAmbitCnctr_AcctInfo ===============");
                    // Setup the target parameters
                    setupConfig(reqObj);
                    Log("DEBUG", "Request XML from CCM: " + reqObj.xmlDoc); // TODO: Should never be added.
                    // Initialize the username
                    Log("DEBUG", "EmployeeNo: " + reqObj.m_accountName);

                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAmbitCnctr_AcctInfo");
                    // adding basic details

                    List<String> basicDtlsCols = new List<String>();
                    basicDtlsCols.Add("EMPID");
                    basicDtlsCols.Add("FIRSTNAME");
                    basicDtlsCols.Add("MIDDLENAME");
                    basicDtlsCols.Add("LASTNAME");
                    basicDtlsCols.Add("GENDER");

                    StrList basicDtls = executeReader("select EMPID,FIRSTNAME,MIDDLENAME,LASTNAME,GENDER from ns_empbasicinfo where empid = '" + reqObj.m_accountName + "'", basicDtlsCols, "ADIBAmbitCnctr_AcctInfo");
                    if (basicDtls.Capacity > 0)
                    {
                        mapAttrsToValues.AddParamValue("EmployeeId", basicDtls[0]);
                        mapAttrsToValues.AddParamValue("FirstName", basicDtls[1]);
                        mapAttrsToValues.AddParamValue("MiddleName", basicDtls[2]);
                        mapAttrsToValues.AddParamValue("LastName", basicDtls[3]);
                        mapAttrsToValues.AddParamValue("Gender", basicDtls[4]);
                    }
                    else
                    {
                        mapAttrsToValues.AddParamValue("EmployeeId", reqObj.m_accountName);
                    }

                    Log("Account: " + this.m_sEmployeeNo + " fetched successfully.");
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                }
                finally
                {
                    Log("=============== Out ADIBAmbitCnctr_AcctInfo ===============");
                    respond_acctInfo(resObj, mapAttrsToValues, lstNotAllowed, this.bErr, this.sErrMsg);
                }
            }
        }// ADIBAmbitCnctr_AcctInfo

        public void ADIBAmbitCnctr_AcctCreate(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAmbitCnctr_AcctCreate ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (userExist(reqObj))
                {
                    Log("DEBUG", "User already exist in the Ambit system");
                    respond_acctCreate(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " already exist in the Ambit system");
                }
                respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAmbitCnctr_AcctCreate");
                    Log("Request received to create Ambit User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBAmbitCnctr_AcctCreate");
                    canHaveOnePossibleInput(this.m_sGender, "Can Select Only 1 Gender at a time");
                    canHaveOnePossibleInput(this.m_sBranchCode, "Can Select Only 1 Branch Code at a time");
                    canHaveOnePossibleInput(this.m_sDepartmentCode, "Can Select Only 1 Department Code at a time");
                    canHaveOnePossibleInput(this.m_sUserType, "Can Select Only 1 User Type at a time");
                    canHaveOnePossibleInput(this.m_sLimitType, "Can Select Only 1 Limit Type at a time");
                    canHaveOnePossibleInput(this.m_sAccessCode, "Can Select Only 1 Access Code at a time");
                    createExecStoredProcedure(AmbitStoredProcName);
                    constructSPParameters("I", this.m_sEmployeeNo, this.m_sFirstName, this.m_sMiddleName, this.m_sLastName, this.m_sGender, this.m_sFullName, this.m_sBranchCode, this.m_sDepartmentCode, this.m_sUserType, this.m_sLimitType, this.m_sRoles, this.m_sAccessControlLimit, this.m_sAccessCode);
                    executeStoredProc();
                    object errorMsg = this.command.Parameters["p_err_msg"].Value;
                    if (errorMsg == null || errorMsg.ToString().ToLower().Equals("null"))
                    {
                        Log("DEBUG", "Account successfully added to Ambit System");
                    }
                    else
                    {
                        throw new Exception(errorMsg.ToString());
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBAmbitCnctr_AcctCreate");
                    Log("DEBUG", "=============== Out ADIBAmbitCnctr_AcctCreate ===============");
                    respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }

            }
        } // ADIBAmbitCnctr_AcctCreate

        public void ADIBAmbitCnctr_AcctChange(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                    if (reqObj.m_object == "Password Reset")
                    {
                        respond_statusNotSupported(resObj);
                    }
                    else // Perform change action
                    {
                        Log("DEBUG", "=============== In ADIBAmbitCnctr_AcctChange ===============");
                        setupConfig(reqObj);
                        Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                        try
                        {
                            if (userLocked(reqObj))
                            {
                                throw new Exception("Cannot modify access for Locked User");
                            }
                            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAmbitCnctr_AcctCreate");
                            Log("Request received to create Ambit User : " + reqObj.m_accountName);
                            initializeAttributes(reqObj, "ADIBAmbitCnctr_AcctCreate");
                            createExecStoredProcedure(AmbitStoredProcName);
                            string branchCode = getAttributeValue(reqObj, "BRANCHCODE");
                            string userType = getAttributeValue(reqObj, "USERTYPE");
                            string limitType = getAttributeValue(reqObj, "LIMITTYPE");
                            constructSPParameters("U", this.m_sEmployeeNo, null, null, null, null, null, branchCode, null, userType, limitType, this.m_sRoles, null, null);
                            executeStoredProc();
                            object errorMsg = this.command.Parameters["p_err_msg"].Value;
                            if (errorMsg == null || errorMsg.ToString().ToLower().Equals("null"))
                            {
                                Log("DEBUG", "Account successfully updated in Ambit System");
                            }
                            else
                            {
                                throw new Exception(errorMsg.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            SetExceptionMessage(ex);
                        }
                        finally
                        {
                            Log("DEBUG", "=============== Out ADIBAmbitCnctr_AcctChange ===============");
                            closeConnection("ADIBAmbitCnctr_AcctChange");
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
                    Log("DEBUG", "=============== Out ADIBAmbitCnctr_AcctChange ===============");
                    closeConnection("ADIBAmbitCnctr_AcctChange");
                    respond_acctChange(resObj, this.m_sEmployeeNo, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBAmbitCnctr_AcctChange

        public void ADIBAmbitCnctr_AcctEnable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAmbitCnctr_AcctEnable ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (!userExist(reqObj))
                {
                    Log("INFO", "User doesn't exist in the Ambit system");
                    respond_acctEnable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " doesn't exist in the Ambit system");
                }
                if (!userLocked(reqObj))
                {
                    Log("INFO", "User is already unlocked in the Ambit system");
                    respond_acctEnable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " is already unlocked in the Ambit system");
                }
                respond_acctEnable(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAmbitCnctr_AcctEnable");
                    Log("Request received to create Ambit User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBAmbitCnctr_AcctEnable");
                    executeCommand("UPDATE ns_userchannelinfo SET status = 'NEW' WHERE userid = " + reqObj.m_accountName, null, "ADIBAmbitCnctr_AcctEnable");
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBAmbitCnctr_AcctEnable");
                    Log("DEBUG", "=============== Out ADIBAmbitCnctr_AcctEnable ===============");
                    respond_acctEnable(resObj, this.m_sEmployeeNo, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBAmbitCnctr_AcctEnable

        public void ADIBAmbitCnctr_AcctDisable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAmbitCnctr_AcctDisable ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (!userExist(reqObj))
                {
                    Log("DEBUG", "User doesn't exist in the Ambit system");
                    respond_acctDisable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " doesn't exist in the Ambit system");
                }
                if (userLocked(reqObj))
                {
                    Log("DEBUG", "User is already locked in the Ambit system");
                    respond_acctDisable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " is already locked in the Ambit system");
                }
                respond_acctDisable(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBAmbitCnctr_AcctCreate");
                    Log("Request received to create Ambit User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBAmbitCnctr_AcctDisable");
                    createExecStoredProcedure(AmbitStoredProcName);
                    constructSPParameters("D", this.m_sEmployeeNo, null, null, null, null, null, null, null, null, null, null, null, null);
                    executeStoredProc();
                    object errorMsg = this.command.Parameters["p_err_msg"].Value;
                    if (errorMsg == null || errorMsg.ToString().ToLower().Equals("null"))
                    {
                        Log("DEBUG", "Account successfully disabled in Ambit System");
                    }
                    else
                    {
                        throw new Exception(errorMsg.ToString());
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBAmbitCnctr_AcctDisable");
                    Log("DEBUG", "=============== Out ADIBAmbitCnctr_AcctDisable ===============");
                    respond_acctDisable(resObj, this.m_sEmployeeNo, false, this.m_sEmployeeNo + " account disabled successfully");
                }

            }
        } // ADIBAmbitCnctr_AcctDisable

        public void ADIBAmbitCnctr_AcctDelete(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBAmbitCnctr_AcctDelete ===============");
            respond_statusNotSupported(resObj);
            Log("INFO", "=============== Out ADIBAmbitCnctr_AcctDelete ===============");
        } // ADIBAmbitCnctr_AcctDELETE

        public override void AssignSupportedScriptFunctions()
        {
            base.RedirectInterface(COUR_INTERFACE_VALIDATE_TARGET_CONFIG, ADIBAmbitCnctr_ValidateTargetConfig, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CREATE, ADIBAmbitCnctr_AcctCreate, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CHANGE, ADIBAmbitCnctr_AcctChange, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_INFO, ADIBAmbitCnctr_AcctInfo, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DISABLE, ADIBAmbitCnctr_AcctDisable, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_ENABLE, ADIBAmbitCnctr_AcctEnable, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DELETE, ADIBAmbitCnctr_AcctDelete, true, true, false);
        }

    }
}
