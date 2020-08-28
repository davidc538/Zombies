using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace RustLib.Decorators
{
	public class DecoratorManager
	{
		public List<BaseDecorator> decorators = new List<BaseDecorator>();

		[JsonIgnore]
		public IRoot root = null;

		[JsonIgnore]
		public object owning_state;

		[JsonIgnore]
		public Action<object> debug;

		public T GetOwner<T>() where T : class
		{
			if (owning_state is T)
				return owning_state as T;

			return default(T);
		}

		public void AddDecorator(BaseDecorator decorator)
		{
			decorators.Add(decorator);
			decorator.Initialize();
			decorator.SetOwner(this);
		}

		public void RemoveComponent<T>()
		{
			List<BaseDecorator> to_remove = new List<BaseDecorator>();

			foreach (BaseDecorator decorator in decorators)
				if (decorator is T)
					to_remove.Add(decorator);

			foreach (BaseDecorator decorator in to_remove)
				RemoveComponent(decorator);
		}

		public void RemoveComponent(BaseDecorator decorator)
		{
			decorator.OnUnload();
			decorators.Remove(decorator);
		}

		public void RequireInvoke(string method_name, params object[] parameters)
		{
			bool has_recipient = HasRecipient(method_name);

			if (!has_recipient)
				root?.Log($"DecoratorManager: No recipient for {method_name}!");

			Invoke(method_name, parameters);
		}

		public void Invoke(string method_name, params object[] parameters)
		{
			UpdateOwners();

			foreach (BaseDecorator component in decorators)
			{
				component.Initialize();

				if (component.HasPublicMethod(method_name))
					component.Invoke(method_name, parameters);
			}
		}

		public T Return<T>(string method_name, params object[] parameters)
		{
			UpdateOwners();

			foreach (BaseDecorator component in decorators)
			{
				MethodInfo info = component.GetPublicMethod(method_name);
				component.SetOwner(this);

				if (info?.ReturnType == typeof(T))
				{
					object obj = info.Invoke(component, parameters);

					if (obj is T)
						return (T)obj;
				}
			}

			return default(T);
		}

		protected bool HasRecipient(string method_name)
		{
			UpdateOwners();

			foreach (BaseDecorator decorator in decorators)
				if (decorator.HasPublicMethod(method_name))
					return true;

			return false;
		}

		protected void UpdateOwners()
		{
			foreach (BaseDecorator decorator in decorators)
				decorator.SetOwner(this);
		}

		protected void Debug(object message)
		{
			root?.Log(message.ToString());
		}
	}
}
