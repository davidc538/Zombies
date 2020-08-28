using System;
using System.Collections.Generic;
using System.Reflection;

namespace RustLib.Decorators
{
	public class BaseDecorator
	{
		private List<MethodInfo> methods = new List<MethodInfo>();
		private DecoratorManager owner = null;

		public void SetOwner(DecoratorManager new_owner)
		{
			owner = new_owner;
		}

		public DecoratorManager GetOwner()
		{
			return owner;
		}

		public void Initialize()
		{
			if (methods.Count > 0)
				return;

			Type type = this.GetType();

			methods.AddRange(type.GetMethods());
		}

		public MethodInfo GetPublicMethod(string method_name)
		{
			foreach (MethodInfo info in methods)
				if (info.Name == method_name)
					return info;

			return null;
		}

		public bool HasPublicMethod(string method_name)
		{
			MethodInfo info = GetPublicMethod(method_name);

			if (info == null)
				return false;

			return true;
		}

		public void Invoke(string method_name, object[] parameters)
		{
			MethodInfo info = GetPublicMethod(method_name);

			if (info == null)
				return;

			info.Invoke(this, parameters);
		}

		protected void Debug(object message)
		{
			GetOwner()?.root?.Log(message.ToString());
		}

		public virtual void OnUnload()
		{

		}
	}
}
