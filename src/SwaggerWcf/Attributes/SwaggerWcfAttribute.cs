﻿using System;

namespace SwaggerWcf.Attributes
{
    /// <summary>
    ///     Attribute to enable a class or interface to be scanned by SwaggerWcf
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class SwaggerWcfAttribute : Attribute
    {
        /// <summary>
        ///     Export this service on Swagger file
        /// </summary>
        /// <param name="servicePath">Service path</param>
        public SwaggerWcfAttribute(string servicePath, string name = null)
        {
            ServicePath = servicePath;
            Name = name;
        }

        /// <summary>
        ///     Path of this service
        /// </summary>
        public string ServicePath { get; set; }

        /// <summary>
        ///     Name of this servce
        /// </summary>
        public string Name { get; set; }
    }
}
