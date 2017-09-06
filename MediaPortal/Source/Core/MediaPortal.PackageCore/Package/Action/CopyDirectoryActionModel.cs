﻿#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using System.Diagnostics;
using MediaPortal.Common.Logging;

namespace MediaPortal.PackageCore.Package.Action
{
  [DebuggerDisplay("CopyDirectory({Source}->{Target}, RootCanExist={TargetRootCanExist}, Overwrite={Overwrite})")]
  partial class CopyDirectoryActionModel
  {
    #region Overrides of ActionBaseModel

    public override string ElementsName
    {
      get { return "CopyDirectory"; }
    }

    public override bool CheckElements(ILogger log)
    {
      return base.CheckElements(log) &
        this.CheckNotNullOrEmpty(Source, "Source", log) &
        this.CheckNotNullOrEmpty(Target, "Target", log);
    }

    public override void Execute(InstallContext context)
    {
      var sourcePath = context.GetPackagePath(Parent.Parent, Source);
      Package.EnsureDirectoryExtracted(sourcePath);
      FileHelper.CopyDirectory(context, 
        sourcePath, 
        context.GetPath(Target), 
        Overwrite, TargetRootCanExist, true, true);
    }

    #endregion
  }
}