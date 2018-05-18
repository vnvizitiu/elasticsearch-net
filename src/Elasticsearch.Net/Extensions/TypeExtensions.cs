﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Elasticsearch.Net
{
	internal static class TypeExtensions
	{
		public delegate T ObjectActivator<out T>(params object[] args);

		private static readonly MethodInfo GetActivatorMethodInfo =
			typeof(TypeExtensions).GetMethod(nameof(GetActivator), BindingFlags.Static | BindingFlags.NonPublic);

		private static readonly ConcurrentDictionary<string, ObjectActivator<object>> CachedActivators =
			new ConcurrentDictionary<string, ObjectActivator<object>>();

		internal static T CreateInstance<T>(this Type t, params object[] args) => (T) t.CreateInstance(args);

		internal static object CreateInstance(this Type t, params object[] args)
		{
			ObjectActivator<object> activator;
			var argKey = args.Length;
			var key = argKey + "--" + t.FullName;
			if (CachedActivators.TryGetValue(key, out activator))
				return activator(args);

			var generic = GetActivatorMethodInfo.MakeGenericMethod(t);
			var constructors = from c in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
							   let p = c.GetParameters()
							   let k = string.Join(",", p.Select(a => a.ParameterType.Name))
							   where p.Length == args.Length
							   select c;

			var ctor = constructors.FirstOrDefault();
			if (ctor == null)
				throw new Exception($"Cannot create an instance of {t.FullName} because it has no constructor taking {args.Length} arguments");
			activator = (ObjectActivator<object>) generic.Invoke(null, new[] {ctor});
			CachedActivators.TryAdd(key, activator);
			return activator(args);
		}
		internal static bool IsValueType(this Type type)
		{
			return type.GetTypeInfo().IsValueType;
		}

		//do not remove this is referenced through GetActivatorMethod
		private static ObjectActivator<T> GetActivator<T>(ConstructorInfo ctor)
		{
			var type = ctor.DeclaringType;
			var paramsInfo = ctor.GetParameters();

			//create a single param of type object[]
			var param = Expression.Parameter(typeof(object[]), "args");

			var argsExp = new Expression[paramsInfo.Length];

			//pick each arg from the params array
			//and create a typed expression of them
			for (int i = 0; i < paramsInfo.Length; i++)
			{
				var index = Expression.Constant(i);
				var paramType = paramsInfo[i].ParameterType;

				var paramAccessorExp = Expression.ArrayIndex(param, index);

				var paramCastExp = Expression.Convert(paramAccessorExp, paramType);

				argsExp[i] = paramCastExp;
			}

			//make a NewExpression that calls the
			//ctor with the args we just created
			var newExp = Expression.New(ctor, argsExp);

			//create a lambda with the New
			//Expression as body and our param object[] as arg
			var lambda = Expression.Lambda(typeof(ObjectActivator<T>), newExp, param);

			//compile it
			var compiled = (ObjectActivator<T>) lambda.Compile();
			return compiled;
		}
	}
}
