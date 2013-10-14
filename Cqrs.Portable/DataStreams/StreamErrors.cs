#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 

// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence

#endregion

using System;
using System.Globalization;

namespace Lokad.Cqrs.StreamingStorage
{
    /// <summary>
    /// Helper class for throwing storage exceptions in a consistent way.
    /// </summary>
    public static class StreamErrors
    {

        public static Exception ContainerNotFound(IStreamContainer item, Exception inner = null)
        {
            var message = string.Format(CultureInfo.InvariantCulture, "Storage container was not found: '{0}'.",
                item.FullPath);
            return new StreamContainerNotFoundException(message, inner);
        }
    }
}