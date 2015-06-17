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
// Date: 02 June 2015
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
using Sybase.Data.AseClient;
using System.Security.Cryptography;
using System.Xml;

namespace ADIBTradewindConnector
{
    public class ADIBTradewindCnctr : RDKCore
    {
        // Target Validation Parameters
        private string m_sHost;
        private string m_sPort;
        private string m_sADIBTFDatabaseName;
        private string m_sATMDatabaseName;
        private string m_sMasterDatabaseName;
        private string m_sPhoenixDatabaseName;
        private string m_sDBUserName;
        private string m_sDBPassword;

        // connection objects
        private AseConnection connection;

        // Define the Log object and required variables
        private COURPROFILERLib.CourProfileLog2Class _log_obj = null;
        private string _log_file;
        private bool _log_level = true;
        private int iConnectionTimeout = 15;
        private int iRetryCount = 0;

        // Supporting variables
        private bool bErr = false;
        private string sErrMsg;

        // Operational Parameters
        private string m_sUsername;
        private string m_sUserRole;
        private string m_sOperatorId;
        private string m_sOperatorName;
        private string m_sFirstName;
        private string m_sLastName;
        private string m_sOperatorInitials;
        private string m_sLoginId;
        private string m_sDBUser;
        private string m_sForcePassChange;
        private string m_sOperatorSuspend;
        private string m_sExpirationDate;
        private string m_sSuspenseClearDate;
        private string m_sDefintBranchId;
        private string m_sTransrestrict;
        private string m_sSecurityLevel;
        private string m_sProductId;
        private bool m_bUnlockOnly;
        private const char multiValueSeprator = ',';
        private AseTransaction idmTransaction = null;

        enum ApplicationType
        {
            ADIBTF,
            ATM,
            MASTER,
            PHOENIX
        }

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

        public ADIBTradewindCnctr()
            : base("CourionTradewindCnctr")
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
                attributesFile = !string.IsNullOrEmpty(config.AppSettings.Settings["AttributeXML"].Value) ? config.AppSettings.Settings["AttributeXML"].Value : "ADIBTradewindAttributes.xml";
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
                this.m_sADIBTFDatabaseName = req.GetParameter("ADIBTFDatabaseName");
                this.m_sATMDatabaseName = req.GetParameter("ATMDatabaseName");
                this.m_sMasterDatabaseName = req.GetParameter("MasterDatabaseName");
                this.m_sPhoenixDatabaseName = req.GetParameter("PhoenixDatabaseName");
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

        private AseConnection getDatabaseConnection(RequestObject req, ApplicationType value)
        {
            setupConfig(req);
            string databaseName = "";
            AseConnection conn = null;
            switch (value.ToString())
            {
                case "ADIBTF":
                    databaseName = this.m_sADIBTFDatabaseName;
                    break;
                case "ATM":
                    databaseName = this.m_sATMDatabaseName;
                    break;
                case "MASTER":
                    databaseName = this.m_sMasterDatabaseName;
                    break;
                case "PHOENIX":
                    databaseName = this.m_sPhoenixDatabaseName;
                    break;
                default:
                    Log("ERROR", "Incorrect value of ApplicationType passed in getDatabaseConnection method : " + value.ToString());
                    break;
            }
            if (databaseName.Length > 0)
            {
                conn = makeConnection(this.m_sHost, this.m_sPort, databaseName, this.m_sDBUserName, this.m_sDBPassword, "getDatabaseConnection");
            }
            return conn;
        }

        // This method opens the connection with Oracle database
        private AseConnection makeConnection(String host, String port, String databaseName, String username, String password, String methodName)
        {
            Log("DEBUG", "Trying to establish connection to the Tradewind database for method " + methodName);
            Log("DEBUG", "retryCount = " + iRetryCount + " connect timout= " + iConnectionTimeout);
            //Log("DEBUG", "Parameters host : " + host + " port : "+port + " serviceName : "+serviceName + " username : "+username +" password : "+password);  
            this.connection = new AseConnection();
            try
            {
                String connString = "Data Source='" + this.m_sHost + "';Port='" + this.m_sPort + "';UID='" + m_sDBUserName + "';PWD='" + m_sDBPassword + "';Database='" + databaseName + "';";

                connection.ConnectionString = connString;

                connection.Open();
                Log("INFO", "Successfully connected to the Tradewind database for method " + methodName);
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to " + databaseName + " database for method " + methodName + " :: " + e.Message);
                throw e;
            }
            return this.connection;
        }

        // This method closes the connection with Sybase database
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
                Log("ERROR", "Error while connecting to Tradewind database :: " + e.Message);
                throw e;
            }

        }

        // This method closes the connection with Sybase database
        private void closeConnection(AseConnection conn, String methodName)
        {
            Log("DEBUG", "Closing database connection for method " + methodName);
            try
            {
                conn.Close();
                Log("INFO", "Connection closed successfully for method " + methodName);
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while connecting to Tradewind database :: " + e.Message);
                throw e;
            }

        }

        private List<List<object>> getResultsFromSqlDb(AseConnection conn, string query)
        {
            List<List<object>> resultSet = new List<List<object>>();
            Log("INFO", "Select Query to be executed : " + query);
            try
            {
                AseCommand command = new AseCommand(query, conn);
                AseDataReader reader = command.ExecuteReader();
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
            foreach (List<object> row in resultSet)
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

        private int setResultsToSqlDb(AseConnection conn, string query)
        {
            int rowsEffected = -1;
            Log("INFO", "Query to be executed : " + query);
            try
            {
                AseCommand command = conn.CreateCommand();
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

        private int setResultsToSqlDbWithRoleback(AseConnection conn, string query, AseTransaction transaction)
        {
            int rowsEffected = -1;
            Log("DEBUG", "Inside method setResultsToSqlDbWithRoleback");
            Log("DEBUG", "Query to be executed : " + query);
            try
            {
                AseCommand command = conn.CreateCommand();
                command.CommandText = query;
                rowsEffected = command.ExecuteNonQuery();
                Log("DEBUG", "Number of rows effected : " + rowsEffected);
            }
            catch (Exception e)
            {
                Log("DEBUG", "Exception occurred --- " + e.Message);
                transaction.Rollback();
                throw e;
            }
            return rowsEffected;
        }

        private void addLogin(AseConnection conn, string loginame, string passwd, string defdbstr, string deflanguagestr, string fullnamestr)
        {
            AseCommand cmd = conn.CreateCommand();
            cmd.CommandText = "sp_addlogin";
            cmd.CommandType = CommandType.StoredProcedure;

            AseParameter loginName = new AseParameter();
            loginName.ParameterName = "@loginame";
            loginName.AseDbType = AseDbType.VarChar;
            loginName.Direction = ParameterDirection.Input;
            loginName.Value = loginame;
            cmd.Parameters.Add(loginName);

            AseParameter pwd = new AseParameter();
            pwd.ParameterName = "@passwd";
            pwd.AseDbType = AseDbType.VarChar;
            pwd.Direction = ParameterDirection.Input;
            pwd.Value = passwd;
            cmd.Parameters.Add(pwd);

            AseParameter defdb = new AseParameter();
            defdb.ParameterName = "@defdb";
            defdb.AseDbType = AseDbType.VarChar;
            defdb.Direction = ParameterDirection.Input;
            defdb.Value = defdbstr;
            cmd.Parameters.Add(defdb);

            AseParameter deflanguage = new AseParameter();
            deflanguage.ParameterName = "@deflanguage";
            deflanguage.AseDbType = AseDbType.VarChar;
            deflanguage.Direction = ParameterDirection.Input;
            deflanguage.Value = deflanguagestr;
            cmd.Parameters.Add(deflanguage);

            AseParameter fullname = new AseParameter();
            fullname.ParameterName = "@fullname";
            fullname.AseDbType = AseDbType.VarChar;
            fullname.Direction = ParameterDirection.Input;
            fullname.Value = fullnamestr;
            cmd.Parameters.Add(fullname);

            cmd.ExecuteNonQuery();
        }

        private void assignRole(AseConnection conn, string rolenamestr, string loginamestr)
        {
            AseCommand cmd = conn.CreateCommand();
            cmd.CommandText = "sp_role";
            cmd.CommandType = CommandType.StoredProcedure;

            AseParameter action = new AseParameter();
            action.ParameterName = "@action";
            action.AseDbType = AseDbType.VarChar;
            action.Direction = ParameterDirection.Input;
            action.Value = "grant";
            cmd.Parameters.Add(action);

            AseParameter rolename = new AseParameter();
            rolename.ParameterName = "@rolename";
            rolename.AseDbType = AseDbType.VarChar;
            rolename.Direction = ParameterDirection.Input;
            rolename.Value = rolenamestr;
            cmd.Parameters.Add(rolename);

            AseParameter loginame = new AseParameter();
            loginame.ParameterName = "@grantee";
            loginame.AseDbType = AseDbType.VarChar;
            loginame.Direction = ParameterDirection.Input;
            loginame.Value = loginamestr;
            cmd.Parameters.Add(loginame);

            cmd.ExecuteNonQuery();
        }


        private void modifyLogin(AseConnection conn, string loginame, string optionstr, string valuestr)
        {
            AseCommand cmd = conn.CreateCommand();
            cmd.CommandText = "sp_modifylogin";
            cmd.CommandType = CommandType.StoredProcedure;

            AseParameter loginName = new AseParameter();
            loginName.ParameterName = "@loginame";
            loginName.AseDbType = AseDbType.VarChar;
            loginName.Direction = ParameterDirection.Input;
            loginName.Value = loginame;
            cmd.Parameters.Add(loginName);

            AseParameter option = new AseParameter();
            option.ParameterName = "@option";
            option.AseDbType = AseDbType.VarChar;
            option.Direction = ParameterDirection.Input;
            option.Value = optionstr;
            cmd.Parameters.Add(option);

            AseParameter value = new AseParameter();
            value.ParameterName = "@value";
            value.AseDbType = AseDbType.VarChar;
            value.Direction = ParameterDirection.Input;
            value.Value = valuestr;
            cmd.Parameters.Add(value);

            cmd.ExecuteNonQuery();
        }

        private void addUser(AseConnection conn, string loginame, string nameInDbStr, string userGroup)
        {
            AseCommand cmd = conn.CreateCommand();
            cmd.CommandText = "sp_adduser";
            cmd.CommandType = CommandType.StoredProcedure;

            AseParameter loginName = new AseParameter();
            loginName.ParameterName = "@loginame";
            loginName.AseDbType = AseDbType.VarChar;
            loginName.Direction = ParameterDirection.Input;
            loginName.Value = loginame;
            cmd.Parameters.Add(loginName);

            AseParameter nameInDb = new AseParameter();
            nameInDb.ParameterName = "@name_in_db";
            nameInDb.AseDbType = AseDbType.VarChar;
            nameInDb.Direction = ParameterDirection.Input;
            nameInDb.Value = nameInDbStr;
            cmd.Parameters.Add(nameInDb);

            AseParameter grpname = new AseParameter();
            grpname.ParameterName = "@grpname";
            grpname.AseDbType = AseDbType.VarChar;
            grpname.Direction = ParameterDirection.Input;
            grpname.Value = userGroup;
            cmd.Parameters.Add(grpname);

            cmd.ExecuteNonQuery();
        }

        private int getNextIdNum(AseConnection conn)
        {
            string query = "SELECT NextIDNum FROM NewIDNum WHERE NameIdNum = 'OPERATOR'";
            try
            {
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                object numObj = results[0][0];
                int nextIdNum = Convert.ToInt32(numObj.ToString());
                Log("DEBUG", "NextIDNum = " + nextIdNum);
                return nextIdNum;
            }
            catch (Exception e)
            {
                Log("ERROR", "Exception : Unable to get Next Id Num");
                throw e;
            }
        }

        private int getOperatorId(AseConnection conn, string userID)
        {
            string query = "SELECT OPERATORID FROM OPERATOR WHERE LOGINID = '" + userID + "'";
            try
            {
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                object numObj = results[0][0];
                int nextIdNum = Convert.ToInt32(numObj.ToString());
                Log("DEBUG", "OPERATORID = " + nextIdNum);
                return nextIdNum;
            }
            catch (Exception e)
            {
                Log("ERROR", "Exception : Unable to get Operator ID");
                throw e;
            }
        }

        private void insertValuesForUser(RequestObject reqObj, AseConnection adibtfConn, Boolean isCreate, int nextIdNum)
        {
            if (!isCreate)
            {
                this.idmTransaction = adibtfConn.BeginTransaction(IsolationLevel.ReadCommitted);
            }
            if (this.m_sUserRole.ToUpper().Equals("PROCESSOR"))
            {
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 4 )", this.idmTransaction); //Product Operator

                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 115)", this.idmTransaction); //Export Bill Collection
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 120)", this.idmTransaction); //Export LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 100)", this.idmTransaction); //Export Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 20)", this.idmTransaction); //Import Bill Collection
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 10)", this.idmTransaction); //Import LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 0)", this.idmTransaction); //Import Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 65)", this.idmTransaction); //Inward Standby LC
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 63)", this.idmTransaction); //Inward Standby LC Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 155)", this.idmTransaction); //LC Reimbursement
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 84)", this.idmTransaction); //Miscellaneuos
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 50)", this.idmTransaction); //Outward Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 53)", this.idmTransaction); //Outward Guarantee Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 60)", this.idmTransaction); //Outward Standby LC
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 62)", this.idmTransaction); //Outward Standby LC Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 40)", this.idmTransaction); //Shipping Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 150)", this.idmTransaction); //Templates
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 140)", this.idmTransaction); //Transfer Letter of Credit
            }
            else if (this.m_sUserRole.ToUpper().Equals("VERIFIER"))
            {
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 2 )", this.idmTransaction); //Division Supervisor
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 3 )", this.idmTransaction); //Product Supervisor

                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 115)", this.idmTransaction); //Export Bill Collection
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 120)", this.idmTransaction); //Export LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 100)", this.idmTransaction); //Export Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 20)", this.idmTransaction); //Import Bill Collection
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 10)", this.idmTransaction); //Import LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 0)", this.idmTransaction); //Import Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 65)", this.idmTransaction); //Inward Standby LC
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 63)", this.idmTransaction); //Inward Standby LC Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 155)", this.idmTransaction); //LC Reimbursement
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 84)", this.idmTransaction); //Miscellaneuos
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 50)", this.idmTransaction); //Outward Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 53)", this.idmTransaction); //Outward Guarantee Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 60)", this.idmTransaction); //Outward Standby LC
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 62)", this.idmTransaction); //Outward Standby LC Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 40)", this.idmTransaction); //Shipping Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 150)", this.idmTransaction); //Templates
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 140)", this.idmTransaction); //Transfer Letter of Credit
            }
            else if (this.m_sUserRole.ToUpper().Equals("LIMIT FEEDER"))
            {
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 12 )", this.idmTransaction); //Credit

                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 120)", this.idmTransaction); //Export LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 100)", this.idmTransaction); //Export Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 20)", this.idmTransaction); //Import Bill Collection
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 10)", this.idmTransaction); //Import LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 0)", this.idmTransaction); //Import Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 50)", this.idmTransaction); //Outward Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 53)", this.idmTransaction); //Outward Guarantee Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 60)", this.idmTransaction); //Outward Standby LC
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 62)", this.idmTransaction); //Outward Standby LC Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 40)", this.idmTransaction); //Shipping Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 150)", this.idmTransaction); //Templates
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 140)", this.idmTransaction); //Transfer Letter of Credit
            }
            else if (this.m_sUserRole.ToUpper().Equals("ADMINISTRATOR"))
            {
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 2 )", this.idmTransaction); //Division Supervisor
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 6 )", this.idmTransaction); //Super User
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 0 )", this.idmTransaction); //Security Master

                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 120)", this.idmTransaction); //Export LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 100)", this.idmTransaction); //Export Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 20)", this.idmTransaction); //Import Bill Collection
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 10)", this.idmTransaction); //Import LC Negotiation
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 0)", this.idmTransaction);  //Import Letter of Credit
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 50)", this.idmTransaction); //Outward Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 53)", this.idmTransaction); //Outward Guarantee Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 60)", this.idmTransaction); //Outward Standby LC
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 62)", this.idmTransaction); //Outward Standby LC Claim
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 40)", this.idmTransaction); //Shipping Guarantee
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 150)", this.idmTransaction); //Templates
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERPRODUCTS ( OPERATORID , INTBRANCHID , PRODUCTID ) VALUES ( " + nextIdNum + " , 0 , 140)", this.idmTransaction); //Transfer Letter of Credit
            }
            else if (this.m_sUserRole.ToUpper().Equals("AUDITOR"))
            {
                setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORLEVEL ( OPERATORID , INTBRANCHID , SECURITYLEVELID ) VALUES ( " + nextIdNum + ", 0 , 1 )", this.idmTransaction); //Auditor
            }
            this.idmTransaction.Commit();
        }

        // initialize the attributes
        private void initializeAttributes(RequestObject reqObj, String methodName)
        {
            Log("DEBUG", "Initializing the attributes for method " + methodName);
            this.m_sUsername = 'T' + reqObj.m_accountName;
            this.m_sUserRole = reqObj.GetParameter("UserRole");
            this.m_sOperatorId = reqObj.GetParameter("OperatorId");
            this.m_sOperatorName = reqObj.GetParameter("OperatorName");
            this.m_sFirstName = reqObj.GetParameter("FirstName");
            this.m_sLastName = reqObj.GetParameter("LastName");
            this.m_sOperatorInitials = reqObj.GetParameter("OperatorInitials");
            this.m_sLoginId = reqObj.GetParameter("LoginId");
            this.m_sDBUser = reqObj.GetParameter("DBUser");
            this.m_sDBUser = reqObj.GetParameter("DBUser");
            this.m_sForcePassChange = reqObj.GetParameter("ForcePassChange");
            this.m_sOperatorSuspend = reqObj.GetParameter("OperatorSuspend");
            this.m_sExpirationDate = reqObj.GetParameter("ExpirationDate");
            this.m_sSuspenseClearDate = reqObj.GetParameter("SuspenseClearDate");
            this.m_sDefintBranchId = reqObj.GetParameter("DefintBranchId");
            this.m_sTransrestrict = reqObj.GetParameter("Transrestrict");
            this.m_sSecurityLevel = reqObj.GetParameter("SecurityLevel");
            this.m_sProductId = reqObj.GetParameter("ProductId");
            Log("DEBUG", "Attributes Initialized successfully for method " + methodName);
        }

        // check weather the user for which the Action takes place exist or not
        private Boolean userExist(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user exist in the Tradewind system"); // TODO: Should never be added.
            // Initialize the username
            initializeAttributes(reqObj, "userExist");
            Log("DEBUG", "UserName: " + reqObj.m_accountName);
            AseConnection conn = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
            List<List<object>> result = getResultsFromSqlDb(conn, "SELECT OPERATORSUSPENDED FROM OPERATOR WHERE LOGINID = '" + reqObj.m_accountName + "'");
            if (result.Count > 0)
            {
                closeConnection(conn, "userExist");
                return true;
            }
            closeConnection(conn, "userExist");
            return false;
        }

        // check weather the user login for which the Action takes place exist or not
        private Boolean userLoginExist(RequestObject reqObj)
        {
            setupConfig(reqObj);
            Log("DEBUG", "Checking if the user login exist in the Tradewind system"); // TODO: Should never be added.
            // Initialize the username
            initializeAttributes(reqObj, "userLoginExist");
            Log("DEBUG", "UserName: S" + reqObj.m_accountName + "TW");
            AseConnection conn = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
            List<List<object>> result = getResultsFromSqlDb(conn, "select name as user_name from sysusers where name = 'S" + reqObj.m_accountName + "TW'");
            if (result.Count > 0)
            {
                closeConnection(conn, "userLoginExist");
                return true;
            }
            closeConnection(conn, "userLoginExist");
            return false;
        }

        private Boolean isUserActive(RequestObject reqObj)
        {
            if (userExist(reqObj))
            {
                Log("DEBUG", "Checking if the user is active in the Tradewind system"); // TODO: Should never be added.
                AseConnection conn = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
                List<List<object>> result = getResultsFromSqlDb(conn, "SELECT OPERATORSUSPENDED FROM OPERATOR WHERE LOGINID = '" + reqObj.m_accountName + "'");
                foreach (List<object> row in result)
                {
                    foreach (object cell in row)
                    {
                        if (cell.ToString().Equals("0"))
                        {
                            Log("INFO", "User - " + this.m_sUsername + " is Active in Tradewind system");
                            return true;
                        }
                        else
                        {
                            Log("INFO", "User - " + this.m_sUsername + " is InActive in Tradewind system");
                            return false;
                        }
                    }
                }
            }
            else
            {
                Log("INFO", "User - " + this.m_sUsername + " is not present in Tradewind system");
            }
            return false;
        }

        // returns the operator details
        private StrList getOperatorDetails(RequestObject req, int index)
        {
            Log("DEBUG", "Getting operator details from index " + index);
            StrList list = new StrList();
            object value;
            try
            {
                string query = "SELECT OPERATORID , OPERATORNAME , OPERATORINITIALS , LOGINID , DBUSER , FORCEPASSCHANGE , OPERATORSUSPENDED , EXPIRATIONDATE , SUSPENSECLEARDATE , DEFINTBRANCHID , TRANRESTRICT FROM OPERATOR where LOGINID = '" + req.m_accountName + "'";
                AseConnection conn = getDatabaseConnection(req, ApplicationType.ADIBTF);
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                Log("INFO", "Operator details query :: " + query);
                if (results.Capacity > 0)
                {
                    value = results[0][index];
                    Log("DEBUG", "Returning operator value :  " + value);
                    if (value == null)
                    {
                        list.Add("NULL");
                    }
                    else
                    {
                        list.Add(value.ToString());
                    }
                }
                return list;
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while fetching operator details for user " + req.m_accountName + " - Error Message " + e.Message);
                throw new Exception("Failed to fetch Operator details for User Please Contact System Administrator");
            }
        }

        // get Security Level List
        private StrList getSecurityLevelList(RequestObject req)
        {
            Log("DEBUG", "Getting security level list");
            StrList grpList = new StrList();
            try
            {
                string query = "SELECT sl.SECURITYLEVELNAME FROM SECURITYLEVEL sl , OPERATORLEVEL ol WHERE sl.SECURITYLEVELID = ol.SECURITYLEVELID AND ol.OPERATORID = (SELECT OPERATORID FROM OPERATOR WHERE LOGINID = '" + req.m_accountName + "') AND ol.INTBRANCHID = 0 ORDER BY UPPER ( SECURITYLEVELNAME )";
                AseConnection conn = getDatabaseConnection(req, ApplicationType.ADIBTF);
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                Log("INFO", "Groups details query :: " + query);
                foreach (List<object> rows in results)
                {
                    foreach (object cell in rows)
                    {
                        grpList.Add(cell.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while fetching security level list " + req.m_accountName + " - Error Message " + e.Message);
                throw new Exception("Failed to fetch Security Level List. Please Contact System Administrator");
            }
            Log("DEBUG", "Returning security level list " + grpList.Count);
            return grpList;
        }


        private StrList getProductNameList(RequestObject req)
        {
            Log("DEBUG", "Getting product name list");
            StrList grpList = new StrList();
            try
            {
                string query = "SELECT pr.PRODUCTNAME FROM PRODUCT pr , USERPRODUCTS upr WHERE pr.PRODUCTID = upr.PRODUCTID AND upr.OPERATORID = (SELECT OPERATORID FROM OPERATOR WHERE LOGINID = '" + req.m_accountName + "') AND upr.INTBRANCHID = 0 ORDER BY UPPER ( PRODUCTNAME )";
                AseConnection conn = getDatabaseConnection(req, ApplicationType.ADIBTF);
                List<List<object>> results = getResultsFromSqlDb(conn, query);
                Log("INFO", "Groups details query :: " + query);
                foreach (List<object> rows in results)
                {
                    foreach (object cell in rows)
                    {
                        if (cell == null)
                        {
                            grpList.Add("NULL");
                        }
                        else
                        {
                            grpList.Add(cell.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while fetching product name list " + req.m_accountName + " - Error Message " + e.Message);
                throw new Exception("Failed to fetch Product Name List. Please Contact System Administrator");
            }
            Log("DEBUG", "Returning product name list " + grpList.Count);
            return grpList;
        }

        private StrList getUserRoleList(RequestObject req)
        {
            Log("DEBUG", "Getting user role list");
            StrList grpList = new StrList();
            StrList secLevel = new StrList();
            try
            {
                secLevel = getSecurityLevelList(req);
                int roleId = 0;
                string abrevation = "";
                if (secLevel.Capacity > 0)
                {
                    foreach (string securityLevel in secLevel)
                    {
                        if (securityLevel.Equals("Product Operator"))
                        {
                            roleId++;
                            abrevation = "PO";
                        }
                        else if (securityLevel.Equals("Division Supervisor"))
                        {
                            roleId++;
                        }
                        else if (securityLevel.Equals("Product Supervisor"))
                        {
                            roleId++;
                        }
                        else if (securityLevel.Equals("Credit"))
                        {
                            roleId++;
                            abrevation = "CR";
                        }
                        else if (securityLevel.Equals("Auditor"))
                        {
                            roleId++;
                            abrevation = "AU";
                        }
                        else if (securityLevel.Equals("Super User"))
                        {
                            roleId++;
                        }
                        else if (securityLevel.Equals("Security Master"))
                        {
                            roleId++;
                        }
                    }
                    if (roleId == 3)
                    {
                        grpList.Add("ADMINISTRATOR");
                    }
                    else if (roleId == 2)
                    {
                        grpList.Add("VERIFIER");
                    }
                    else if (roleId == 1 && abrevation.Equals("PO"))
                    {
                        grpList.Add("PROCESSOR");
                    }
                    else if (roleId == 1 && abrevation.Equals("CR"))
                    {
                        grpList.Add("LIMIT FEEDER");
                    }
                    else if (roleId == 1 && abrevation.Equals("AU"))
                    {
                        grpList.Add("AUDITOR");
                    }
                }
            }
            catch (Exception e)
            {
                Log("ERROR", "Error while fetching product name list " + req.m_accountName + " - Error Message " + e.Message);
                throw new Exception("Failed to fetch Product Name List. Please Contact System Administrator");
            }
            Log("DEBUG", "Returning product name list " + grpList.Count);
            return grpList;
        }

        public void ADIBTradewindCnctr_ValidateTargetConfig(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBTradewindCnctr_ValidateTargetConfig ===============");
            try
            {
                // Setup the target parameters
                setupConfig(reqObj);
                Log("INFO", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    AseConnection adibtf = makeConnection(this.m_sHost, this.m_sPort, this.m_sADIBTFDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBTradewindCnctr_ValidateTargetConfig");
                    AseConnection atm = makeConnection(this.m_sHost, this.m_sPort, this.m_sATMDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBTradewindCnctr_ValidateTargetConfig");
                    AseConnection master = makeConnection(this.m_sHost, this.m_sPort, this.m_sMasterDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBTradewindCnctr_ValidateTargetConfig");
                    AseConnection phoenix = makeConnection(this.m_sHost, this.m_sPort, this.m_sPhoenixDatabaseName, this.m_sDBUserName, this.m_sDBPassword, "ADIBTradewindCnctr_ValidateTargetConfig");
                    Log("INFO", "Target validated successfully.");
                    closeConnection(adibtf, "ADIBTradewindCnctr_ValidateTargetConfig");
                    closeConnection(atm, "ADIBTradewindCnctr_ValidateTargetConfig");
                    closeConnection(master, "ADIBTradewindCnctr_ValidateTargetConfig");
                    closeConnection(phoenix, "ADIBTradewindCnctr_ValidateTargetConfig");
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + " EXCEPTION");
                }

            }
            catch (Exception ex)
            {
                SetExceptionMessage(ex);
                Log("INFO", "Target validation failed.");
            }
            finally
            {
                Log("INFO", "=============== Out ADIBTradewindCnctr_ValidateTargetConfig ===============");
                respond_validateTargetConfiguration(resObj, this.bErr, this.sErrMsg);
            }
        } // ADIBTradewindCnctr_ValidateTargetConfig

        public void ADIBTradewindCnctr_EnableUser(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBTradewindCnctr_EnableUser ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                // Respond with not supported
                respond_statusNotSupported(resObj);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    if (!userExist(reqObj))
                    {
                        Log("INFO", "User doesn't exist in the Tradewind system");
                        throw new Exception("User doesn't exist in the Tradewind system");
                    }
                    if (isUserActive(reqObj))
                    {
                        Log("INFO", "User is already unlocked in the Tradewind system");
                        throw new Exception("User is already unlocked in the Tradewind system");
                    }
                    this.m_sUsername = reqObj.m_accountName;
                    AseConnection conn = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
                    String query = "UPDATE OPERATOR SET OPERATORSUSPENDED = 0 where LOGINID='" + this.m_sUsername + "'";
                    setResultsToSqlDb(conn, query);
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBTradewindCnctr_EnableUser");
                    Log("DEBUG", "=============== Out ADIBTradewindCnctr_EnableUser ===============");
                    respond_acctEnable(resObj, this.m_sUsername, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBTradewindCnctr_EnableUser

        public void ADIBTradewindCnctr_DisableUser(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBTradewindCnctr_DisableUser ===============");
            if ((methodType == COUR_METHOD_TYPE_PREPROCESS) || (methodType == COUR_METHOD_TYPE_POSTPROCESS))
            {
                // Respond with not supported
                respond_statusNotSupported(resObj);
            }
            else
            {
                setupConfig(reqObj);
                Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                try
                {
                    if (!userExist(reqObj))
                    {
                        Log("INFO", "User doesn't exist in the Tradewind system");
                        throw new Exception("User doesn't exist in the Tradewind system");
                    }
                    if (!isUserActive(reqObj))
                    {
                        Log("INFO", "User is already locked in the Tradewind system");
                        throw new Exception("User is already locked in the Tradewind system");
                    }
                    this.m_sUsername = reqObj.m_accountName;
                    AseConnection conn = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
                    String query = "UPDATE OPERATOR SET OPERATORSUSPENDED = 1 where LOGINID='" + this.m_sUsername + "'";
                    setResultsToSqlDb(conn, query);
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBTradewindCnctr_DisableUser");
                    Log("DEBUG", "=============== Out ADIBTradewindCnctr_DisableUser ===============");
                    respond_acctDisable(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBTradewindCnctr_DisableUser

        public void ADIBTradewindCnctr_AcctInfo(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                    Log("INFO", "=============== In ADIBTradewindCnctr_AcctInfo ===============");
                    // Setup the target parameters
                    setupConfig(reqObj);
                    Log("DEBUG", "Request XML from CCM: " + reqObj.xmlDoc); // TODO: Should never be added.
                    // Initialize the username
                    Log("DEBUG", "UserName: " + reqObj.m_accountName);

                    this.connection = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
                    // adding basic details

                    // initilizing the attributes
                    initializeAttributes(reqObj, "ADIBTradewindCnctr_AcctInfo");

                    // adding operator details for User to show
                    mapAttrsToValues.SetParamValues("OperatorId", getOperatorDetails(reqObj, 0));
                    mapAttrsToValues.SetParamValues("OperatorName", getOperatorDetails(reqObj, 1));
                    mapAttrsToValues.SetParamValues("OperatorInitials", getOperatorDetails(reqObj, 2));
                    mapAttrsToValues.SetParamValues("LoginId", getOperatorDetails(reqObj, 3));
                    mapAttrsToValues.SetParamValues("DBUser", getOperatorDetails(reqObj, 4));
                    mapAttrsToValues.SetParamValues("ForcePassChange", getOperatorDetails(reqObj, 5));
                    mapAttrsToValues.SetParamValues("OperatorSuspend", getOperatorDetails(reqObj, 6));
                    mapAttrsToValues.SetParamValues("ExpirationDate", getOperatorDetails(reqObj, 7));
                    mapAttrsToValues.SetParamValues("SuspenseClearDate", getOperatorDetails(reqObj, 8));
                    mapAttrsToValues.SetParamValues("DefintBranchId", getOperatorDetails(reqObj, 9));
                    mapAttrsToValues.SetParamValues("Transrestrict", getOperatorDetails(reqObj, 10));

                    // adding security level for User to show
                    mapAttrsToValues.SetParamValues("SecurityLevel", getSecurityLevelList(reqObj));
                    // adding product name for User to show
                    mapAttrsToValues.SetParamValues("ProductName", getProductNameList(reqObj));
                    // adding role for User to show
                    mapAttrsToValues.SetParamValues("UserRole", getUserRoleList(reqObj));
                    Log("Account: " + reqObj.m_accountName + " fetched successfully.");
                }
                catch (Exception ex)
                {
                    SetExceptionMessage(ex);
                }
                finally
                {
                    Log("=============== Out ADIBTradewindCnctr_AcctInfo ===============");
                    respond_acctInfo(resObj, mapAttrsToValues, lstNotAllowed, this.bErr, this.sErrMsg);
                }
            }
        }// ADIBTradewindCnctr_AcctInfo


        public void ADIBTradewindCnctr_AcctCreate(RequestObject reqObj, ResponseObject resObj, string methodType)
        {
            Log("INFO", "=============== In ADIBTradewindCnctr_AcctCreate ===============");
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
                    Log("Request received to create Tradewind User : " + 'S' + reqObj.m_accountName + "TW");
                    initializeAttributes(reqObj, "ADIBTradewindCnctr_AcctCreate");
                    if (!userLoginExist(reqObj))
                    {
                        if (this.m_sUserRole.Equals(""))
                        {
                            throw new Exception("Please select 1 Role to provision the User in Tradewind System");
                        }
                        else if (this.m_sUserRole.Split(multiValueSeprator).Length > 1)
                        {
                            throw new Exception("Cannot select more than 1 Role for a User in Tradewind System");
                        }
                        AseConnection masterConn = getDatabaseConnection(reqObj, ApplicationType.MASTER);
                        addLogin(masterConn, "S" + reqObj.m_accountName + "TW", "adib1234", this.m_sATMDatabaseName, "us_english", this.m_sFirstName + " " + this.m_sLastName);
                        assignRole(masterConn, "TW_USER", "S" + reqObj.m_accountName + "TW");
                        modifyLogin(masterConn, "S" + reqObj.m_accountName + "TW", "add default role", "TW_USER");
                        closeConnection(masterConn, "ADIBTradewindCnctr_AcctCreate");
                        AseConnection atmConn = getDatabaseConnection(reqObj, ApplicationType.ATM);
                        addUser(atmConn, "S" + reqObj.m_accountName + "TW", "S" + reqObj.m_accountName + "TW", "AGRP_TW");
                        closeConnection(atmConn, "ADIBTradewindCnctr_AcctCreate");
                        AseConnection phoenixConn = getDatabaseConnection(reqObj, ApplicationType.PHOENIX);
                        addUser(phoenixConn, "S" + reqObj.m_accountName + "TW", "S" + reqObj.m_accountName + "TW", "AGRP_TW");
                        closeConnection(phoenixConn, "ADIBTradewindCnctr_AcctCreate");
                        AseConnection adibtfConn = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
                        addUser(adibtfConn, "S" + reqObj.m_accountName + "TW", "S" + reqObj.m_accountName + "TW", "public");
                        this.idmTransaction = adibtfConn.BeginTransaction(IsolationLevel.ReadCommitted);
                        int nextIdNum = getNextIdNum(adibtfConn);
                        DateTime curDate = DateTime.Now;
                        DateTime futureDate = curDate.AddYears(1);
                        string strDate = futureDate.ToString();
                        Log("DEBUG", "End date will be : " + strDate);
                        setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATOR ( OPERATORID , OPERATORNAME , OPERATORINITIALS , LOGINID , PASSWRD , DBUSER , DBPASSWRD , FORCEPASSCHANGE , OPERATORSUSPENDED , EXPIRATIONDATE , SUSPENSECLEARDATE , DEFINTBRANCHID , TRANRESTRICT ) VALUES (" + nextIdNum + " ,'" + this.m_sFirstName + " " + this.m_sLastName + "','" + this.m_sFirstName.Substring(0, 1) + this.m_sLastName.Substring(0, 1) + "','S" + reqObj.m_accountName + "TW','412622124605AFBB' , 'S" + reqObj.m_accountName + "TW','412622124605AFBB',1,0,'" + strDate + "',NULL,0,0)", this.idmTransaction);
                        setResultsToSqlDbWithRoleback(adibtfConn, "UPDATE NewIDNum SET NextIDNum = NextIDNum + 1 WHERE NameIdNum = 'OPERATOR'", this.idmTransaction);
                        setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO OPERATORBRANCH ( OPERATORID , INTBRANCHID ) VALUES ( " + nextIdNum + " , 0 )", this.idmTransaction);
                        setResultsToSqlDbWithRoleback(adibtfConn, "INSERT INTO USERWORKGROUP ( OPERATORID , INTBRANCHID , WORKGROUPID ) VALUES ( " + nextIdNum + " , 0 , 0 )", this.idmTransaction);

                        insertValuesForUser(reqObj, adibtfConn, true, nextIdNum);
                    }
                    else
                    {
                        throw new Exception("User Already Exists in Tradewind System");
                    }
                }
                catch (Exception e)
                {
                    SetExceptionMessage(e);
                }
                finally
                {
                    closeConnection("ADIBTradewindCnctr_AcctCreate");
                    Log("DEBUG", "=============== Out ADIBTradewindCnctr_AcctCreate ===============");
                    respond_acctCreate(resObj, "S" + reqObj.m_accountName + "TW", this.bErr, this.sErrMsg);
                }

            }
        } // ADIBTradewindCnctr_AcctCreate


        public void ADIBTradewindCnctr_AcctChange(RequestObject reqObj, ResponseObject resObj, string methodType)
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
                    if (!userExist(reqObj))
                    {
                        throw new Exception("User doesn't exist in tradewind system.");
                    }
                    if (!isUserActive(reqObj))
                    {
                        throw new Exception("User is disabled in Tradewind System. Cannot change any attribute for disabled user!");
                    }
                    AseConnection conn = getDatabaseConnection(reqObj, ApplicationType.ADIBTF);
                    Log("DEBUG", reqObj.m_object);
                    if (reqObj.m_object == "Password Reset")
                    {
                        Log("DEBUG", "=============== In ADIBTradewindCnctr_AcctChange Password Reset ===============");
                        // Read the configured value of 'UnlockOnly'. If it is not set to 'true' or 'false' throw exception
                        if (reqObj.GetParameter("UnlockOnly") == null)
                            throw new Exception("'UnlockOnly' is not configured.");

                        sUnlockOnly = reqObj.GetParameter("UnlockOnly");

                        Log("DEBUG", "UnlockOnly Flag = " + sUnlockOnly);

                        if ((sUnlockOnly.ToUpper() != "TRUE") && (sUnlockOnly.ToUpper() != "FALSE"))
                            throw new Exception("Invalid value configured for 'UnlockOnly'. It must either be 'true' or 'false'.");

                        this.m_bUnlockOnly = bool.Parse(sUnlockOnly);

                        if (this.m_bUnlockOnly)
                        {
                            // Only Unlock the account. No password reset performed.
                            // Set not required parameters to default value, as they will be required to build the XML
                            String query = "UPDATE OPERATOR SET OPERATORSUSPENDED = 1 where LOGINID='" + reqObj.m_accountName + "'";
                            setResultsToSqlDb(conn, query);
                        }
                        else
                        {
                            DateTime curDate = DateTime.Now;
                            DateTime futureDate = curDate.AddYears(1);
                            string strDate = futureDate.ToString();

                            String query = "UPDATE OPERATOR SET PASSWRD = '412622124605AFBB',  FORCEPASSCHANGE = 1, EXPIRATIONDATE = '" + strDate + "' where LOGINID='" + reqObj.m_accountName + "'";
                            setResultsToSqlDb(conn, query);
                            Log("INFO", "Password reset for user, " + reqObj.m_accountName + " successfully performed and updated in Tradewind System.");
                        }
                    }
                    else // Perform change action
                    {
                        Log("DEBUG", "=============== In ADIBTradewindCnctr_AcctChange ===============");
                        setupConfig(reqObj);
                        Log("DEBUG", "Request XML from CCM: " + reqObj); // TODO: Should never be added.
                        try
                        {
                            if (!isUserActive(reqObj))
                            {
                                throw new Exception("Cannot change access for a Disabled User");
                            }
                            if (this.m_sUserRole.Equals(""))
                            {
                                throw new Exception("Please select 1 Role to provision the User in Tradewind System");
                            }
                            else if (this.m_sUserRole.Split(multiValueSeprator).Length > 1)
                            {
                                throw new Exception("Cannot select more than 1 Role for a User in Tradewind System");
                            }
                            Log("Request received to create Tradewind User : " + reqObj.m_accountName + "");
                            initializeAttributes(reqObj, "ADIBTradewindCnctr_AcctChange");
                            int nextIdNum = getOperatorId(conn, reqObj.m_accountName);
                            string query = "DELETE FROM OPERATORLEVEL WHERE OPERATORID=" + nextIdNum;
                            setResultsToSqlDb(conn, query);
                            query = "DELETE FROM USERPRODUCTS WHERE OPERATORID=" + nextIdNum;
                            setResultsToSqlDb(conn, query);
                            insertValuesForUser(reqObj, conn, false, nextIdNum);
                            Log("DEBUG", "User Access Successfully changed!!");
                        }
                        catch (Exception ex)
                        {
                            SetExceptionMessage(ex);
                        }
                        finally
                        {
                            Log("DEBUG", "=============== Out ADIBTradewindCnctr_AcctChange ===============");
                            closeConnection("ADIBTradewindCnctr_AcctChange");
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
                    Log("DEBUG", "=============== Out ADIBTradewindCnctr_AcctChange ===============");
                    closeConnection("ADIBTradewindCnctr_AcctChange");
                    respond_acctChange(resObj, reqObj.m_accountName, this.bErr, this.sErrMsg);
                }
            }
        } // ADIBTradewindCnctr_AcctChange

        public override void AssignSupportedScriptFunctions()
        {
            base.RedirectInterface(COUR_INTERFACE_VALIDATE_TARGET_CONFIG, ADIBTradewindCnctr_ValidateTargetConfig, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_INFO, ADIBTradewindCnctr_AcctInfo, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CREATE, ADIBTradewindCnctr_AcctCreate, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_CHANGE, ADIBTradewindCnctr_AcctChange, false, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_ENABLE, ADIBTradewindCnctr_EnableUser, true, true, false);
            base.RedirectInterface(COUR_INTERFACE_ACCT_DISABLE, ADIBTradewindCnctr_DisableUser, true, true, false);
        }
    }
}
