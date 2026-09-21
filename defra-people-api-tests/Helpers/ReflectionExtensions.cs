using System;
using System.Reflection;
using System.Threading.Tasks;

namespace defra.people_api_tests.Helpers
{
    /// <summary>
    /// Extension methods to help with reflection in unit tests
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Invokes a private method on an object using reflection
        /// </summary>
        /// <typeparam name="T">The return type of the method</typeparam>
        /// <param name="obj">The object instance to call the method on</param>
        /// <param name="methodName">The name of the private method</param>
        /// <param name="parameters">The parameters to pass to the method</param>
        /// <returns>The result of the method invocation</returns>
        public static T InvokePrivateMethod<T>(this object obj, string methodName, params object[] parameters)
        {
            var type = obj.GetType();
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (method == null)
                throw new ArgumentException($"Method '{methodName}' not found on type '{type.FullName}'");
                
            return (T)method.Invoke(obj, parameters);
        }
        
        /// <summary>
        /// Invokes a private async method on an object using reflection
        /// </summary>
        /// <param name="obj">The object instance to call the method on</param>
        /// <param name="methodName">The name of the private method</param>
        /// <param name="parameters">The parameters to pass to the method</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task InvokePrivateMethodAsync(this object obj, string methodName, params object[] parameters)
        {
            var task = (Task)InvokePrivateMethod<object>(obj, methodName, parameters);
            await task;
        }
        
        /// <summary>
        /// Invokes a private async method on an object using reflection and returns a result
        /// </summary>
        /// <typeparam name="T">The return type of the method</typeparam>
        /// <param name="obj">The object instance to call the method on</param>
        /// <param name="methodName">The name of the private method</param>
        /// <param name="parameters">The parameters to pass to the method</param>
        /// <returns>The result of the asynchronous operation</returns>
        public static async Task<T> InvokePrivateMethodAsync<T>(this object obj, string methodName, params object[] parameters)
        {
            var task = (Task<T>)InvokePrivateMethod<object>(obj, methodName, parameters);
            return await task;
        }
    }
}
