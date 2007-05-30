using System;
using System.Diagnostics;
using System.Expressions;
using System.IO;
using System.Reflection;
using System.Text;
using C5;

namespace LinqToRdf
{
	public class QuerySupertype<T>{
		protected NamespaceManager namespaceManager = new NamespaceManager();
		protected TextWriter logger;
		protected Type originalType = typeof(T);
		protected Delegate projection;
		protected HashSet<MemberInfo> properties = new HashSet<MemberInfo>();
		protected string query;
		protected QueryFactory<T> queryFactory;

		public TextWriter Logger
		{
			get { return logger; }
			set { logger = value; }
		}

		public Type OriginalType
		{
			get { return originalType; }
			set { originalType = value; }
		}

		public HashSet<MemberInfo> Properties
		{
			get { return properties; }
			set { properties = value; }
		}

		public Delegate Projection
		{
			get { return projection; }
			set { projection = value; }
		}

		public string Query
		{
			get { return query; }
			set { query = value; }
		}

		public QueryFactory<T> QueryFactory
		{
			get { return queryFactory; }
			set { queryFactory = value; }
		}

		protected void BuildProjection(Expression expression)
		{
			LambdaExpression le = ((MethodCallExpression)expression).Parameters[1] as LambdaExpression;
			if (le == null) throw new ApplicationException("Incompatible expression type found when building a projection");
			projection = le.Compile();
			MemberInitExpression mie = le.Body as MemberInitExpression;
			if (mie != null)
				foreach (Binding b in mie.Bindings)
					FindProperties(b);
			else
				foreach (PropertyInfo i in originalType.GetProperties())
					properties.Add(i);
		}

		private void FindProperties(Binding e)
		{
			namespaceManager.RegisterType(OriginalType);
			switch (e.BindingType)
			{
				case BindingType.MemberAssignment:
					properties.Add(e.Member);
					break;
				case BindingType.MemberListBinding:
					break;
				case BindingType.MemberMemberBinding:
					properties.Add(e.Member);
					break;
			}
		}

		internal void Log(string msg, params object[] args)
		{
			Trace.WriteLine(string.Format("+ :"+msg, args));
		}

	}
}