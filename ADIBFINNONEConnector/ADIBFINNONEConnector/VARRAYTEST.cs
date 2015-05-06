namespace ADIBFINNONEConnector
{
    using System;
    using Oracle.DataAccess.Client;
    using Oracle.DataAccess.Types;
    using System.Xml.Serialization;
    using System.Xml.Schema;
    // test
    public class VARRAYTEST : IOracleCustomType, INullable
    {
        [OracleArrayMapping()]
        public string[] Array { get; set; }

        public VARRAYTEST()
        {
            Array = null;
        }

        public VARRAYTEST(string[] array)
        {
            Array = array;
        }

        #region INullable Members

        public bool IsNull { get { return Array == null; } }

        #endregion

        #region IOracleCustomType Members

        public void FromCustomObject(OracleConnection con, System.IntPtr pUdt)
        {
            OracleUdt.SetValue(con, pUdt, 0, Array);
        }

        public void ToCustomObject(OracleConnection con, System.IntPtr pUdt)
        {
            Array = (string[])OracleUdt.GetValue(con, pUdt, 0);
        }

        public override string ToString()
        {
            if (IsNull)
                return "NULL";
            else
            {
                return "VARRAYTEST('" + String.Join("', '", Array) + "')";
            }
        }

        #endregion
    }

    /* VARRAYTESTFactory Class
    **   An instance of the VARRAYTESTFactory class is used to create 
    **   VARRAYTEST objects
    */
    [OracleCustomTypeMapping("FINSSO.VARRAYTEST")]
    public class VARRAYTESTFactory : IOracleCustomTypeFactory, IOracleArrayTypeFactory
    {
        #region IOracleCustomTypeFactory Members

        public IOracleCustomType CreateObject()
        {
            return new VARRAYTEST();
        }

        #endregion

        #region IOracleArrayTypeFactory Members

        public System.Array CreateArray(int numElems)
        {
            return new String[numElems];
        }

        public System.Array CreateStatusArray(int numElems)
        {
            return new OracleUdtStatus[numElems];
        }

        #endregion
}
}