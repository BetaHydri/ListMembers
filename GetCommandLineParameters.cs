using System;
using System.Collections;
using System.Reflection;

namespace CommandLineParameters
{
	#region Public class Arg
	public class Arg
	{
		private string m_Name = "";
		private string m_Value = "";

		public Arg(string Name, string Value)
		{
			m_Name = Name;
			m_Value = Value;
		}

		public Arg()
		{}

		public string Name
		{
			get {return m_Name;}
			set {m_Name = value;}
		}

		public string Value
		{
			get {return m_Value;}
			set {m_Value = value;}
		}
	}
#endregion

	#region ArgsDictionary
	public class ArgsDictionary : DictionaryBase
	{
		public Arg this[string key]
		{
			get { return (Arg) this.Dictionary[key]; }

			set { this.Dictionary[key] = value; }
		}

		public void Add(string key, Arg arg)
		{
			this.Dictionary.Add(key, arg);
		}

		public bool Contains(string key)
		{
			return this.Dictionary.Contains(key);
		}

		public ICollection Keys
		{
			get { return this.Dictionary.Keys; }
		}
	}
	#endregion

	#region GetCommandLineParameters
	public class GetCommandLineParameters
	{
		private string[] args;
		private int argsCount;

		public string GroupName;
		public bool Recursive;

		private ArgsDictionary ArgsDict;



        #region TransferArgs
        public void TransferArgs(Type ObjectType, object ObjectInstance)
		{
			PropertyInfo[] PInfo = ObjectType.GetProperties(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance);
			PropertyInfo[] ThisPInfo = this.GetType().GetProperties(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance);

			for (int i = 0; i < PInfo.Length; i++)
			{ //for each property
                if (PInfo[i].GetSetMethod() != null) //check if there is a Set method to be able to set the the stored information
                {
                    Console.WriteLine("Found variable " + PInfo[i].Name);
                    //if property is string, bool or int
                    if ((PInfo[i].PropertyType == typeof(string)) | (PInfo[i].PropertyType == typeof(bool)) | (PInfo[i].PropertyType == typeof(int))
                        && (PInfo[i].Name.StartsWith("arg")))
                        if (ArgsDict.Contains(PInfo[i].Name.ToLower().Substring(3)))
                        {
                            object temp = ArgsDict[PInfo[i].Name.ToLower().Substring(3)].Value;

                            if (PInfo[i].PropertyType == typeof(bool))
                            {
                                PInfo[i].SetValue(ObjectInstance, false, null);
                                PInfo[i].SetValue(ObjectInstance, Convert.ToBoolean(temp), null);
                            }
                            if (PInfo[i].PropertyType == typeof(string))
                            {
                                PInfo[i].SetValue(ObjectInstance, "", null);
                                PInfo[i].SetValue(ObjectInstance, Convert.ToString(temp), null);
                            }
                            if (PInfo[i].PropertyType == typeof(int))
                            {
                                PInfo[i].SetValue(ObjectInstance, 0, null);
                                PInfo[i].SetValue(ObjectInstance, Convert.ToInt32(temp), null);
                            }
                        }
                        else
                        {
                            if (PInfo[i].PropertyType == typeof(bool))
                            {
                                PInfo[i].SetValue(ObjectInstance, false, null);
                            }
                            if (PInfo[i].PropertyType == typeof(string))
                            {
                                PInfo[i].SetValue(ObjectInstance, "", null);
                            }
                            if (PInfo[i].PropertyType == typeof(int))
                            {
                                PInfo[i].SetValue(ObjectInstance, 0, null);
                            }
                        }
                }
			}
        }
        #endregion

        public GetCommandLineParameters()
		{ }

        #region PrintParametersText
        public string PrintParametersText()
        {
            string Message = "";

            FieldInfo[] fic = this.GetType().GetFields();
            foreach (FieldInfo fi in fic)
            {
                Message += fi.GetValue(this) == null ?
                fi.Name + ":" + new string(' ', 25 - fi.Name.Length)+ "<not set>" + Environment.NewLine :
                fi.Name + ":" + new string(' ', 25 - fi.Name.Length) + fi.GetValue(this) + Environment.NewLine;
            }
            return Message;
        }
        #endregion

        #region ReadCommandLineParameters
        public int ReadCommandLineParameters(string[] CommandLineArguments, out string Errors)
        {
            Errors = "";
            int Result;
            args = CommandLineArguments;
			argsCount = args.Length;
			ArgsDict = new ArgsDictionary();

            if (CommandLineArguments.Length == 0)
            {
                return 10; //show help
            }

            foreach (string s in args)
            {
                if (s == "/?")
                {
                    return 10; //show help
                }
            }

            Result = SplitParameters();
            if (Result != 0)
            {                
                return Result;
            }

            FieldInfo[] fic = this.GetType().GetFields();

            foreach (FieldInfo fi in fic)
            {
                if (ArgsDict.Contains(fi.Name.ToLower()))
                {
                    try
                    {
                        if (fi.FieldType == typeof(string))
                            this.GetType().GetField(fi.Name).SetValue(this, ArgsDict[fi.Name.ToLower()].Value);

                        if (fi.FieldType == typeof(int))
                            this.GetType().GetField(fi.Name).SetValue(this, Convert.ToInt32(ArgsDict[fi.Name.ToLower()].Value));

                        if (fi.FieldType == typeof(bool))
                            this.GetType().GetField(fi.Name).SetValue(this, Convert.ToBoolean(ArgsDict[fi.Name.ToLower()].Value));
                    }
                    catch
                    {
                        Errors = "Could not convert attribute " + fi.Name + ".";
                        return -1;
                    }
                }
            }
            return 0;
        }
        #endregion

        #region SplitParameters
        private int SplitParameters()
		{
            bool isParameterKnown = false;

			for (int i = 0; i <= (int) args.LongLength - 1; i++)
			{
				Arg arg = new Arg();

				if ( !args[i].StartsWith("/") )
					continue;

				if ( args[i].LastIndexOf(":") > 0 )
				{
					arg.Name = args[i].Substring( 1, args[i].IndexOf(":") - 1 ).ToLower();
					arg.Value = args[i].Substring( args[i].IndexOf(":") + 1 ).ToLower();

                    FieldInfo[] fi = this.GetType().GetFields();
                    foreach (FieldInfo f in fi)
                    {
                        if (arg.Name.ToLower() == f.Name.ToLower())
                            isParameterKnown = true;
                    }

                    if (!isParameterKnown)
                    {
                        return 5; //A Parameter is specified that is unknown
                    }
                    isParameterKnown = false;
				}
				else
				{
					arg.Name = args[i].Substring( 1 ).ToLower();
                    FieldInfo[] fic = this.GetType().GetFields();
                    foreach (FieldInfo fi in fic)
                    {
                        if (fi.Name.ToLower() == arg.Name.ToLower())
                        {
                            if (fi.FieldType == typeof(bool))
                            {
                                arg.Value = "true";
                                break;
                            }
                            else
                            {
                                return 6; //Syntax Error
                            }
                        }
                    }
                }

                try
                {
                    ArgsDict.Add(arg.Name, arg);
                }
                catch
                {
                    Console.WriteLine("A duplicate parameter has been supplied");
                    return -1;
                }
                arg = null;                
			}
            return 0;
        }
        #endregion
    }
	#endregion
}