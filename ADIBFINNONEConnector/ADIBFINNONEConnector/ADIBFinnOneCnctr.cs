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
using Sybase.Data.AseClient;

namespace ADIBFINNONEConnector
{
    public class ADIBFinnOneCnctr : RDKCore
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
        private string m_sPassword;
        private string m_sUsername;
        private string m_sEmail;
        private string m_sDepartment;
        private string m_sBranchProduct;
        private string m_sGroup;
        private string m_sRole;

        // Supporting variables
        private const String FinnOneStoredProcName = "USER_CREATION_PROC1_NEW";
        private bool bErr = false;
        private string sErrMsg;
        private string sResult;
        // Charater used seprate multiple values
        private const char multiValueSeprator = ',';
        private const char branchProductSeprator = '-';
        // Group table details

        private const String groupTableName = "SEC_GROUPS";
        private const String groupIdField = "GROUP_CODE";
        private const String groupNameField = "GROUP_NAME"; 

        // Role table details
        private const String roleTableName = "SEC_MODULES";
        private const String roleIdField = "MODULE_CODE";
        private const String roleNameField = "MODULE_NAME"; 

        // Product table details
        private const String productTableName = "LOS_SEC_OFFICE";
        private const String productIdField = "LSO_OFFICE_CODE_C";
        private const String productNameField = "LSO_OFFICE_NAME_C";

        // Branch table details
        private const String branchTableName = "LOS_SEC_OFFICE";
        private const String branchIdField = "LSO_OFFICE_CODE_C";
        private const String branchNameField = "LSO_OFFICE_NAME_C";

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



        public ADIBFinnOneCnctr()
            : base("CourionFinnOneCnctr")
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
                attributesFile = !string.IsNullOrEmpty(config.AppSettings.Settings["AttributeXML"].Value) ? config.AppSettings.Settings["AttributeXML"].Value : "ADIBFinnOneAttributes.xml";
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
            Log("DEBUG", "Trying to establish connection to the FinnOne database for method " + methodName);
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
                Log("INFO", "Successfully connected to the FinnOne database for method " + methodName);
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to FinnOne database for method " + methodName + " :: " + e.Message);
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
                Log("ERROR", "Error while connecting to FinnOne database :: " + e.Message);
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
        private StrList executeReader(String query,List<String> columns, String methodName)
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
            try
            {
                this.command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void constructSPParameters(String userId, String username, String email, String department, String mode, String rsmId, String[] branchProd, String[] groups, String[] roles)
        {
            addInBuiltInputParameter("VUSERID", OracleDbType.Varchar2, userId);
            addInBuiltInputParameter("VPASSWORD", OracleDbType.Varchar2, "Abc@123");
            addInBuiltInputParameter("VUSERNAME", OracleDbType.Varchar2, username);
            addInBuiltInputParameter("vAddress1", OracleDbType.Varchar2, "Add1");
            addInBuiltInputParameter("VADDRESS2", OracleDbType.Varchar2, "Add2");
            addInBuiltInputParameter("VADDRESS3", OracleDbType.Varchar2, "Add3");
            addInBuiltInputParameter("vAddress4", OracleDbType.Varchar2, "Add4");
            addInBuiltInputParameter("VCOUNTRY", OracleDbType.Varchar2, "United Arab Emirates");
            addInBuiltInputParameter("VSTATE", OracleDbType.Varchar2, "AD");
            addInBuiltInputParameter("VCITY", OracleDbType.Varchar2, "ABUDHABI");
            addInBuiltInputParameter("VZIPCODE", OracleDbType.Varchar2, "201301");
            addInBuiltInputParameter("VCONTPERSON", OracleDbType.Varchar2, "ABC");
            addInBuiltInputParameter("VPHONERESI", OracleDbType.Varchar2, "12345");
            addInBuiltInputParameter("vPhone1Off", OracleDbType.Varchar2, "123456");
            addInBuiltInputParameter("VPHONE2OFF", OracleDbType.Varchar2, "123457");
            addInBuiltInputParameter("VFAXNO", OracleDbType.Varchar2, "44445");
            addInBuiltInputParameter("vPagerNo", OracleDbType.Varchar2, "55555");
            addInBuiltInputParameter("VMOBNO", OracleDbType.Varchar2, "99999");
            addInBuiltInputParameter("VEMAILID", OracleDbType.Varchar2, email);
            addInBuiltInputParameter("VMAKERID", OracleDbType.Varchar2, "MAVCAS1"); // hard-coded value
            addInBuiltInputParameter("VMAKERDATE", OracleDbType.Varchar2, DateTime.Today.ToString("dd-MMM-yyyy"));
            addInBuiltInputParameter("VCHECKERID", OracleDbType.Varchar2, "MAVCAS1"); // hard-coded value
            addInBuiltInputParameter("VCHECKERDATE", OracleDbType.Varchar2, DateTime.Today.ToString("dd-MMM-yyyy"));
            addInBuiltInputParameter("VBRANCHCODE", OracleDbType.Varchar2, department);
            addInBuiltInputParameter("VMODE", OracleDbType.Varchar2, mode);
            addUDTArrayParameter("vBranchProdarr", "FINSSO.VARRAYTEST", OracleDbType.Array, ParameterDirection.Input, branchProd);
            addUDTArrayParameter("VGROUP", "FINSSO.VARRAYTEST", OracleDbType.Array, ParameterDirection.Input, groups);
            addUDTArrayParameter("vModuleRole", "FINSSO.VARRAYTEST", OracleDbType.Array, ParameterDirection.Input, roles);
            addInBuiltInputParameter("VRSMID", OracleDbType.Varchar2, rsmId);
            addInBuiltOutputParameter("v_status", OracleDbType.Varchar2, null,200);
            addInBuiltOutputParameter("v_error_no", OracleDbType.Varchar2, null,20);
            addInBuiltOutputParameter("v_errmsg", OracleDbType.Varchar2, null,2000);
        }

        // returns the Id from the label.
        private String[] getIdsFromLabels(String tableName, String iDFieldName,String labelFieldName, String labels,String category)
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
            return String.Join(multiValueSeprator.ToString(),list.ToArray());
        }
        
        // initialize the attributes
        private void initializeAttributes(RequestObject reqObj, String methodName)
        {
            Log("DEBUG", "Initializing the attributes for method " + methodName);
            this.m_sPassword = reqObj.GetParameter("Password");
            this.m_sUsername = reqObj.GetParameter("Username");
            this.m_sEmail = reqObj.GetParameter("Email");
            this.m_sDepartment = reqObj.GetParameter("Department");
            this.m_sBranchProduct = reqObj.GetParameter("BranchProduct");
            this.m_sGroup = reqObj.GetParameter("Group");
            this.m_sRole = reqObj.GetParameter("Role");
            Log("DEBUG", "Attributes Initialized successfully for method " + methodName);
        }

        // check weather the user for which the Action takes place exist or not
        private Boolean userExist(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user exist in the FinnOne system"); 
            // Initialize the username            
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "userExist");
            OracleCommand selectCmd = new OracleCommand("SELECT USER_ID FROM APPL_USERS_INFO WHERE USER_ID = '" + reqObj.m_accountName + "'", this.connection);
            OracleDataReader reader = null;
            reader = selectCmd.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    if (reader["USER_ID"].Equals(reqObj.m_accountName))
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
            Log("DEBUG", "Checking if the user is locked in the FinnOne system");
            // Initialize the username
            this.m_sUsername = reqObj.m_accountName;
            Log("DEBUG", "UserName: " + this.m_sUsername);
            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "userLocked");
            OracleCommand selectCmd = new OracleCommand("SELECT STATUS from SEC_APPL_USERS WHERE USER_ID = '" + reqObj.m_accountName + "'", this.connection);
            OracleDataReader reader = null;
            reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader["STATUS"].Equals("N"))
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
            this.command = this.connection.CreateCommand();
            this.command.CommandText = spName;
            this.command.CommandType = CommandType.StoredProcedure;
        }

        // returns the RSMID
        private String getRSMIdForUser(String userId)
        {
            String rsmId = "";
            String connetionString = null;
            AseConnection connection;
            AseCommand command = null;
            string sql = null;
            AseDataReader dataReader = null;
            connetionString = "Data Source='10.5.7.196';Port='4002';UID='sa';PWD='welcome';Database='phoenix';";
            sql = "select employee_id from ad_gb_rsm where user_name = 'S" + userId + "'";
            connection = new AseConnection(connetionString);
            try
            {
                Log("INFO","Establishing connection with pheonix database");                
                connection.Open();
                Log("INFO","Connection with pheonix database opened");
                command = new AseCommand(sql, connection);
                dataReader = command.ExecuteReader();
                while (dataReader.Read())
                {
                    rsmId = dataReader.GetValue(0).ToString();
                }
            }
            catch (Exception ex)
            {
                dataReader.Close();
                command.Dispose();
                connection.Close();
                Log("ERROR", "Error getting rsmId from pheonix database" + ex.Message);
                throw new Exception("Failed to connect to pheonix database");
            }
            dataReader.Close();
            command.Dispose();
            connection.Close();
            return rsmId;
        }

        // checking that Roles,Group and BranchProduct are present
        private void checkRoleGroupBranchProdValues(String userId)
        {
            if (this.m_sBranchProduct.Equals("") || this.m_sRole.Equals("") || this.m_sGroup.Equals(""))
            {
                throw new Exception("Branch Product, Role and Group all 3 needs to be given for the User " + userId);
            }
        }

        private void addInBuiltInputParameter(String parameterName,OracleDbType dataType, String value)
        {
            OracleParameter parameter = new OracleParameter();
            parameter.ParameterName = parameterName;
            parameter.OracleDbType = dataType;
            parameter.Direction = ParameterDirection.Input;
            parameter.Value = value;
            this.command.Parameters.Add(parameter);
            Log("INFO", parameter.ParameterName + " :: " + parameter.Value);
            parameter.Dispose();
        }

        private void addInBuiltOutputParameter(String parameterName, OracleDbType dataType, String value,int size)
        {
            OracleParameter parameter = new OracleParameter();
            parameter.ParameterName = parameterName;
            parameter.OracleDbType = dataType;
            parameter.Size = size;
            parameter.Direction = ParameterDirection.Output;
            parameter.Value = value;
            this.command.Parameters.Add(parameter);
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

        public void ADIBFinnOneCnctr_ValidateTargetConfig(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBFinnOneCnctr_ValidateTargetConfig ===============");
            try
            {
                // Setup the target parameters
                setupConfig(reqObj);
                Log("INFO", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBFinnOneCnctr_ValidateTargetConfig");
                    Log("INFO", "Target validated successfully.");
                    closeConnection("ADIBFinnOneCnctr_ValidateTargetConfig");
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
                Log("INFO", "=============== Out ADIBFinnOneCnctr_ValidateTargetConfig ===============");
                respond_validateTargetConfiguration(resObj, this.bErr, this.sErrMsg);
            }
        } // ADIBFinnOneCnctr_ValidateTargetConfig

        public void ADIBFinnOneCnctr_AcctInfo(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                    Log("INFO", "=============== In ADIBFinnOneCnctr_AcctInfo ===============");
                    // Setup the target parameters
                    setupConfig(reqObj);
                    Log("DEBUG", "Request XML from CCM: " + reqObj.xmlDoc); // TODO: Should never be added.
                    // Initialize the username
                    Log("DEBUG", "UserName: " + reqObj.m_accountName);

                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBFinnOneCnctr_AcctInfo");
                    // adding basic details

                    List<String> basicDtlsCols = new List<String>();
                    basicDtlsCols.Add("USER_ID");
                    basicDtlsCols.Add("USER_NAME");
                    basicDtlsCols.Add("STATUS");
                    basicDtlsCols.Add("REMARKS");
                    basicDtlsCols.Add("INIT_FLAG");
                    basicDtlsCols.Add("LOCK_STATUS");
                    basicDtlsCols.Add("EXPIRY_DATE");
                    basicDtlsCols.Add("MAKERID");
                    basicDtlsCols.Add("EMPLOYEE_CODE");
                    basicDtlsCols.Add("SENT_TO_LMS");
                    basicDtlsCols.Add("SENT_TO_COLS");
                    basicDtlsCols.Add("EOD_ACCESS");

                    StrList basicDtls = executeReader("SELECT USER_ID,USER_NAME,STATUS,REMARKS,INIT_FLAG,LOCK_STATUS,EXPIRY_DATE,MAKERID,EMPLOYEE_CODE,SENT_TO_LMS,SENT_TO_COLS,EOD_ACCESS FROM SEC_APPL_USERS WHERE USER_ID = '" + reqObj.m_accountName + "'", basicDtlsCols, "ADIBFinnOneCnctr_AcctInfo");
                    mapAttrsToValues.AddParamValue("UserId", basicDtls[0]);
                    mapAttrsToValues.AddParamValue("Username", basicDtls[1]);
                    mapAttrsToValues.AddParamValue("Status", basicDtls[2]);
                    mapAttrsToValues.AddParamValue("Remarks", basicDtls[3]);
                    mapAttrsToValues.AddParamValue("INIT_FLAG", basicDtls[4]);
                    mapAttrsToValues.AddParamValue("LOCK_STATUS", basicDtls[5]);
                    mapAttrsToValues.AddParamValue("EXPIRY_DATE", basicDtls[6]);
                    mapAttrsToValues.AddParamValue("MAKERID", basicDtls[7]);
                    mapAttrsToValues.AddParamValue("EMPLOYEE_CODE", basicDtls[8]);
                    mapAttrsToValues.AddParamValue("SENT_TO_LMS", basicDtls[9]);
                    mapAttrsToValues.AddParamValue("SENT_TO_COLS", basicDtls[10]);
                    mapAttrsToValues.AddParamValue("EOD_ACCESS", basicDtls[11]);
                    
                    // adding Groups
                    List<String> groupCol = new List<String>();
                    groupCol.Add("GROUP_CODE");
                    StrList groups = executeReader("SELECT GROUP_CODE FROM FINSSO.USER_GROUP_MAP WHERE USER_ID='" + reqObj.m_accountName + "'", groupCol, "ADIBFinnOneCnctr_AcctInfo");
                    mapAttrsToValues.SetParamValues("Group", groups);

                    // adding application groups
                    List<String> appGrpCol = new List<String>();
                    appGrpCol.Add("AppGrp");
                    StrList AppGrp = executeReader("SELECT APPL_CODE || ' - ' || GROUP_CODE AS AppGrp FROM FINSSO.USER_GROUP_MAP WHERE USER_ID='" + reqObj.m_accountName + "'", appGrpCol, "ADIBFinnOneCnctr_AcctInfo");
                    mapAttrsToValues.SetParamValues("ApplicationGroup", AppGrp);

                    // adding Branch Product Values
                    List<String> branchProdCol = new List<String>();
                    branchProdCol.Add("PROD");
                    StrList branchProds = executeReader("SELECT OFFICE_CODE ||'-'|| PRODUCT_CODE AS PROD FROM USER_BRANCH_PROD_MAP WHERE USER_ID='" + reqObj.m_accountName + "'", branchProdCol, "ADIBFinnOneCnctr_AcctInfo");
                    mapAttrsToValues.SetParamValues("BranchProduct", branchProds);
                    
                    
                    // adding department
                    List<String> departmentCol = new List<String>();
                    departmentCol.Add("LSU_LSD_DEPARTMENT_CODE_C");
                    StrList department = executeReader("Select LSU_LSD_DEPARTMENT_CODE_C from finmas.los_sec_user where lsu_user_id_c = '" + reqObj.m_accountName + "'", departmentCol, "ADIBFinnOneCnctr_AcctInfo");
                    mapAttrsToValues.SetParamValues("Department", department);

                    // adding roles
                    List<String> roleCol = new List<String>();
                    roleCol.Add("LSUR_LSR_ROLE_ID_C");
                    StrList roles = executeReader("select LSUR_LSR_ROLE_ID_C from finmas.los_sec_user_roles where LSUR_LSU_USER_ID_C = '" + reqObj.m_accountName + "'", roleCol, "ADIBFinnOneCnctr_AcctInfo");
                    mapAttrsToValues.SetParamValues("Role", roles);

                    Log("Account: " + this.m_sUsername + " fetched successfully.");
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                }
                finally
                {
                    Log("=============== Out ADIBFinnOneCnctr_AcctInfo ===============");
                    respond_acctInfo(resObj, mapAttrsToValues, lstNotAllowed, this.bErr, this.sErrMsg);
                }
            }
        }// ADIBFinnOneCnctr_AcctInfo

        public void ADIBFinnOneCnctr_AcctCreate(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBFinnOneCnctr_AcctCreate ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (userExist(reqObj))
                {
                    Log("DEBUG", "User already exist in the FinnOne system");
                    respond_acctCreate(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " already exist in the FinnOne system");
                }
                respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBFinnOneCnctr_AcctCreate");
                    Log("Request received to create FinnOne User : " + reqObj.m_accountName);
                    initializeAttributes(reqObj, "ADIBFinnOneCnctr_AcctCreate");
                    createExecStoredProcedure(FinnOneStoredProcName);
                    //String[] branchProd = getBranchProdArray();
                    //String[] groupIds = getIdsFromLabels(groupTableName,groupIdField,groupNameField,this.m_sGroup,"GROUP");
                    //String[] roleIds = getIdsFromLabels(roleTableName, roleIdField, roleNameField, this.m_sRole, "ROLE");
                    String rsmId = getRSMIdForUser(reqObj.m_accountName);
                    checkRoleGroupBranchProdValues(reqObj.m_accountName);
                    // adding default role
                    this.m_sRole = "ROLE1," + this.m_sRole;
                    String[] roleIds = this.m_sRole.Split(multiValueSeprator);
                    String[] groupIds = this.m_sGroup.Split(multiValueSeprator);
                    String[] branchProduct = this.m_sBranchProduct.Split(multiValueSeprator);
                    String[] formattedbranchProduct = new String[1];
                    if (branchProduct.Length > 1)
                    {
                        formattedbranchProduct = new String[branchProduct.Length];
                        for (int i = 0; i < branchProduct.Length ; i++)
                        {
                            formattedbranchProduct[i] = branchProduct[i].Replace(branchProductSeprator, multiValueSeprator);
                        }
    
                    }
                    else if (branchProduct.Length == 1)
                    {
                        formattedbranchProduct[0] = this.m_sBranchProduct.Replace(branchProductSeprator, multiValueSeprator);
                    }                    
                    constructSPParameters(reqObj.m_accountName, this.m_sUsername, this.m_sEmail, this.m_sDepartment, "C", rsmId,
                        formattedbranchProduct, groupIds, roleIds);
                    executeStoredProc();
                    if (this.command.Parameters["v_status"].Value.ToString().ToUpper().Equals("ERROR"))
                    {
                        throw new Exception(this.command.Parameters["v_errmsg"].Value.ToString());
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBFinnOneCnctr_AcctCreate");
                    Log("DEBUG", "=============== Out ADIBFinnOneCnctr_AcctCreate ===============");
                    respond_acctCreate(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }

            }
        } // ADIBFinnOneCnctr_AcctCreate

        public void ADIBFinnOneCnctr_AcctChange(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                        Log("DEBUG", "=============== In ADIBFinnOneCnctr_AcctChange ===============");
                        setupConfig(reqObj);
                        Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                        try
                        {
                            makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBFinnOneCnctr_AcctCreate");
                            Log("Request received to create FinnOne User : " + reqObj.m_accountName);
                            initializeAttributes(reqObj, "ADIBFinnOneCnctr_AcctChange");
                            createExecStoredProcedure(FinnOneStoredProcName);
                            String rsmId = getRSMIdForUser(reqObj.m_accountName);
                            checkRoleGroupBranchProdValues(reqObj.m_accountName);
                            // adding default role
                            this.m_sRole = "ROLE1," + this.m_sRole;
                            String[] roleIds = this.m_sRole.Split(multiValueSeprator);
                            String[] groupIds = this.m_sGroup.Split(multiValueSeprator);
                            String[] branchProduct = this.m_sBranchProduct.Split(multiValueSeprator);
                            String[] formattedbranchProduct = null;
                            if (branchProduct.Length > 1)
                            {
                                formattedbranchProduct = new String[branchProduct.Length];
                                for (int i = 0; i < branchProduct.Length; i++)
                                {
                                    formattedbranchProduct[i] = branchProduct[i].Replace(branchProductSeprator, multiValueSeprator);
                                }

                            }
                            constructSPParameters(reqObj.m_accountName, this.m_sUsername, this.m_sEmail, this.m_sDepartment, "U", rsmId,
                                formattedbranchProduct, groupIds, roleIds);
                            executeStoredProc();
                            if (this.command.Parameters["v_status"].Value.ToString().ToUpper().Equals("ERROR"))
                            {
                                throw new Exception(this.command.Parameters["v_errmsg"].Value.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            SetExceptionMessage(ex);
                        }
                        finally
                        {
                            Log("DEBUG", "=============== Out ADIBFinnOneCnctr_AcctChange ===============");
                            closeConnection("ADIBFinnOneCnctr_AcctChange");
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
                    Log("DEBUG", "=============== Out ADIBFinnOneCnctr_AcctChange ===============");
                    closeConnection("ADIBFinnOneCnctr_AcctChange");
                    respond_acctChange(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBFinnOneCnctr_AcctChange

        public void ADIBFinnOneCnctr_AcctEnable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBFinnOneCnctr_AcctEnable ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (!userExist(reqObj))
                {
                    Log("INFO", "User doesn't exist in the FinnOne system");
                    respond_acctEnable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " doesn't exist in the FinnOne system");
                }
                if (!userLocked(reqObj))
                {
                    Log("INFO", "User is already unlocked in the FinnOne system");
                    respond_acctEnable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " is already unlocked in the FinnOne system");
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
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBFinnOneCnctr_AcctEnable");
                    createExecStoredProcedure(FinnOneStoredProcName);
                    constructSPParameters(reqObj.m_accountName, "", "", "", "A", "",
                        null, null, null);
                    executeStoredProc();
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBFinnOneCnctr_AcctEnable");
                    Log("DEBUG", "=============== Out ADIBFinnOneCnctr_AcctEnable ===============");
                    respond_acctEnable(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBFinnOneCnctr_AcctEnable

        public void ADIBFinnOneCnctr_AcctDisable(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBFinnOneCnctr_AcctDisable ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                if (!userExist(reqObj))
                {
                    Log("DEBUG", "User doesn't exist in the FinnOne system");
                    respond_acctDisable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " doesn't exist in the FinnOne system");
                }
                if (userLocked(reqObj))
                {
                    Log("DEBUG", "User is already locked in the FinnOne system");
                    respond_acctDisable(resObj, reqObj.m_accountName, true, reqObj.m_accountName + " is already locked in the FinnOne system");
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
                    makeConnection(this.m_sHost, this.m_sPort, this.m_sServiceName, this.m_sDBUserName, this.m_sDBPassword, "ADIBFinnOneCnctr_AcctDisable");
                    createExecStoredProcedure(FinnOneStoredProcName);
                    constructSPParameters(reqObj.m_accountName, "", "", "", "D", "",
                        null, null, null);
                    executeStoredProc();
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBFinnOneCnctr_AcctDisable");
                    Log("DEBUG", "=============== Out ADIBFinnOneCnctr_AcctDisable ===============");
                    respond_acctDisable(resObj, this.m_sUsername, false, this.m_sUsername + " account disabled successfully");
                }

            }
        } // ADIBFinnOneCnctr_AcctDisable

        public void ADIBFinnOneCnctr_AcctDelete(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBFinnOneCnctr_AcctDelete ===============");
            respond_statusNotSupported(resObj);
            Log("INFO", "=============== Out ADIBFinnOneCnctr_AcctDelete ===============");
        } // ADIBFinnOneCnctr_AcctDELETE

        public override void AssignSupportedScriptFunctions()
        {
            base.RedirectInterface(COUR_INTERFACE_VALIDATE_TARGET_CONFIG, ADIBFinnOneCnctr_ValidateTargetConfig, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CREATE, ADIBFinnOneCnctr_AcctCreate, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CHANGE, ADIBFinnOneCnctr_AcctChange, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_INFO, ADIBFinnOneCnctr_AcctInfo, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DISABLE, ADIBFinnOneCnctr_AcctDisable, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_ENABLE, ADIBFinnOneCnctr_AcctEnable, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DELETE, ADIBFinnOneCnctr_AcctDelete, true, true, false);
        }

    }
}
