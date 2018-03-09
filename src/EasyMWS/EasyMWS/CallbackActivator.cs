﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MountainWarehouse.EasyMWS
{
	public class CallbackActivator
	{

		public Callback SerializeCallback(
			Action<Stream, object> callbackMethod, object callbackData)
		{
			var type = callbackMethod.Method.DeclaringType;
			var method = callbackMethod.Method;
			var dataType = callbackData.GetType();

			return new Callback(type.AssemblyQualifiedName,
				method.Name,
				JsonConvert.SerializeObject(callbackData),
				dataType.AssemblyQualifiedName);
		}

		public void CallMethod(Callback callback)
		{
			var type = Type.GetType(callback.TypeName);
			var method = type.GetMethods().First(mi => mi.Name == callback.MethodName);
			var dataType = Type.GetType(callback.DataTypeName);

			method.Invoke(null, new object[] {null, JsonConvert.DeserializeObject(callback.Data, dataType)});
		}


	}
}