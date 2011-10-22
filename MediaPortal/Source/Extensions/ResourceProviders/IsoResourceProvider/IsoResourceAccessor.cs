﻿#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;
using ISOReader;
using MediaPortal.Utilities;
using MediaPortal.Utilities.FileSystem;

namespace MediaPortal.Extensions.ResourceProviders.IsoResourceProvider
{
  class IsoResourceAccessor : IFileSystemResourceAccessor
  {
    #region Protected fields

    protected IsoResourceProvider _isoProvider;
    internal IsoResourceProxy _isoProxy;
    protected string _pathToDirOrFile;

    protected bool _isDirectory;
    protected DateTime _lastChanged;
    protected long _size;

    #endregion

    #region Ctor

    public IsoResourceAccessor(IsoResourceProvider isoProvider, IsoResourceProxy isoProxy, string pathToDirOrFile)
    {
      if (!pathToDirOrFile.StartsWith("/"))
        throw new ArgumentException("Wrong path '{0}': Path in ISO file must start with a '/' character", pathToDirOrFile);
      _isoProxy = isoProxy;
      _isoProxy.IncUsage();
      _isoProvider = isoProvider;
      _pathToDirOrFile = pathToDirOrFile;

      _isDirectory = true;
      _lastChanged = _isoProxy.IsoFileResourceAccessor.LastChanged;
      _size = -1;

      if (IsEmptyOrRoot)
        return;
      lock (_isoProxy.SyncObj)
      {
        string dosPath = ToDosPath(pathToDirOrFile);
        RecordEntryInfo entry;
        try
        {
          entry = _isoProxy.IsoReader.GetRecordEntryInfo(dosPath); // Path must start with /
        }
        catch
        {
          _isoProxy.DecUsage();
          throw;
        }
        _isDirectory = entry.Directory;
        _lastChanged = entry.Date;
        _size = _isDirectory ? (long) (-1) : entry.Size;
      }
    }

    #endregion

    #region IDisposable implementation

    public void Dispose()
    {
      if (_isoProxy == null)
        return;
      _isoProxy.DecUsage();
      _isoProxy = null;
    }

    #endregion

    #region Protected and internal members

    protected bool IsEmptyOrRoot
    {
      get { return string.IsNullOrEmpty(_pathToDirOrFile) || _pathToDirOrFile == "/"; }
    }

    protected internal static string ToDosPath(string providerPath)
    {
      providerPath = StringUtils.RemovePrefixIfPresent(providerPath, "/");
      return providerPath.Replace('/', Path.DirectorySeparatorChar);
    }

    protected internal static string ToProviderPath(string dosPath)
    {
      string path = dosPath.Replace(Path.DirectorySeparatorChar, '/');
      return StringUtils.CheckPrefix(path, "/");
    }

    protected internal static bool IsResource(IsoReader isoReader, string providerPath)
    {
      string isoPath = ToDosPath(providerPath);
      string dirPath = "\\" + Path.GetDirectoryName(isoPath);
      string isoResource = "\\" + isoPath;

      string[] dirList = isoReader.GetFileSystemEntries(dirPath, SearchOption.TopDirectoryOnly);
      return dirList.Any(entry => entry.Equals(isoResource, StringComparison.OrdinalIgnoreCase));
    }

    protected string ExpandPath(string relativeOrAbsoluteProviderPath)
    {
      return ProviderPathHelper.Combine(_pathToDirOrFile, relativeOrAbsoluteProviderPath);
    }

    #endregion

    #region IResourceAccessor implementation

    public IResourceProvider ParentProvider
    {
      get { return _isoProvider; }
    }

    public bool IsFile
    {
      get { return !_isDirectory; }
    }

    public bool Exists
    {
      get { return true; }
    }

    public string ResourceName
    {
      get
      {
        if (string.IsNullOrEmpty(_pathToDirOrFile))
          return null;
        if (_pathToDirOrFile == "/")
          return _isoProxy.IsoFileResourceAccessor.ResourceName;
        string dosPath = ToDosPath(_pathToDirOrFile);
        return Path.GetFileName(FileUtils.RemoveTrailingPathDelimiter(dosPath));
      }
    }

    public string ResourcePathName
    {
      get { return _isoProxy.IsoFileResourceAccessor.ResourcePathName + " > " + _pathToDirOrFile; }
    }

    public ResourcePath CanonicalLocalResourcePath
    {
      get
      {
        // Abstract from intermediate local FS bridge usage
        return _isoProxy.IsoFileResourceAccessor.CanonicalLocalResourcePath.ChainUp(IsoResourceProvider.ISO_RESOURCE_PROVIDER_ID, _pathToDirOrFile);
      }
    }

    public DateTime LastChanged
    {
      get { return _lastChanged; }
    }

    public long Size
    {
      get { return _size; }
    }

    public void PrepareStreamAccess()
    {
    }

    public Stream OpenRead()
    {
      string dosPath = ToDosPath(_pathToDirOrFile);
      return _isoProxy.IsoReader.GetFileStream(dosPath.StartsWith("\\") ? dosPath : "\\" + dosPath);
    }

    public Stream OpenWrite()
    {
      return null;
    }

    public IResourceAccessor Clone()
    {
      return new IsoResourceAccessor(_isoProvider, _isoProxy, _pathToDirOrFile);
    }

    #endregion

    #region IFileSystemResourceAccessor implementation

    public bool IsDirectory
    {
      get { return _isDirectory; }
    }

    public bool ResourceExists(string path)
    {
      if (path.Equals(_pathToDirOrFile, StringComparison.OrdinalIgnoreCase)) 
        return true;
      return IsResource(_isoProxy.IsoReader, ExpandPath(path));
    }

    public IFileSystemResourceAccessor GetResource(string path)
    {
      string pathToDirOrFile = ExpandPath(path);
      return new IsoResourceAccessor(_isoProvider, _isoProxy, pathToDirOrFile);
    }

    public ICollection<IFileSystemResourceAccessor> GetFiles()
    {
      string dosPath = ToDosPath(_pathToDirOrFile);
      try
      {
        string[] files = _isoProxy.IsoReader.GetFiles(dosPath.StartsWith("\\") ? dosPath : "\\" + dosPath, SearchOption.TopDirectoryOnly);
        return files.Select(path => new IsoResourceAccessor(_isoProvider, _isoProxy,
            ToProviderPath(path))).Cast<IFileSystemResourceAccessor>().ToList();
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Warn("IsoResourceAccessor: Error reading files of '{0}'", e, CanonicalLocalResourcePath);
        return null;
      }
    }

    public ICollection<IFileSystemResourceAccessor> GetChildDirectories()
    {
      string dosPath = ToDosPath(_pathToDirOrFile);
      try
      {
        string[] files = _isoProxy.IsoReader.GetDirectories(dosPath.StartsWith("\\") ? dosPath : "\\" + dosPath, SearchOption.TopDirectoryOnly);
        return files.Select(path => new IsoResourceAccessor(_isoProvider, _isoProxy,
            ToProviderPath(path))).Cast<IFileSystemResourceAccessor>().ToList();
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Warn("IsoResourceAccessor: Error reading child directories of '{0}'", e, CanonicalLocalResourcePath);
        return null;
      }
    }

    #endregion

    public override string ToString()
    {
      return CanonicalLocalResourcePath.ToString();
    }
  }
}
