﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using HttpServer;
using HttpServer.Exceptions;
using HttpServer.Sessions;
using MediaPortal.Backend.MediaLibrary;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.MLQueries;
using MediaPortal.Plugins.MP2Extended.MAS.General;
using Newtonsoft.Json;

namespace MediaPortal.Plugins.MP2Extended.ResourceAccess.MAS.General
{
  class GetMediaItem : IRequestMicroModuleHandler
  {
    public dynamic Process(IHttpRequest request)
    {
      Logger.Info("MAS-GetMediaItem: AbsolutePath: {0}, uriParts.Length: {1}, Lastpart: {2}", request.Uri.AbsolutePath);

      HttpParam httpParam = request.Param;
      if (httpParam["id"].Value == null)
        throw new BadRequestException("GetMediaItem: no id is null");

      ISet<Guid> necessaryMIATypes = new HashSet<Guid>();
      necessaryMIATypes.Add(MediaAspect.ASPECT_ID);
      necessaryMIATypes.Add(ProviderResourceAspect.ASPECT_ID);
      necessaryMIATypes.Add(ImporterAspect.ASPECT_ID);

      ISet<Guid> optionalMIATypes = new HashSet<Guid>();
      optionalMIATypes.Add(VideoAspect.ASPECT_ID);
      optionalMIATypes.Add(MovieAspect.ASPECT_ID);
      optionalMIATypes.Add(SeriesAspect.ASPECT_ID);
      optionalMIATypes.Add(AudioAspect.ASPECT_ID);
      optionalMIATypes.Add(ImageAspect.ASPECT_ID);


      MediaItem item = GetMediaItems.GetMediaItemById(httpParam["id"].Value, necessaryMIATypes, optionalMIATypes);

      if (item == null)
        throw new BadRequestException(String.Format("GetMediaItem: No MediaItem found with id: {0}", httpParam["id"].Value));


      WebMediaItem webMediaItem = new WebMediaItem();
      webMediaItem.Id = item.MediaItemId.ToString();
      // TODO: Add Artwork
      //webMediaItem.Artwork
      webMediaItem.DateAdded = (DateTime)item[ImporterAspect.ASPECT_ID][ImporterAspect.ATTR_DATEADDED];
      //webMediaItem.Path
      webMediaItem.Type = ResourceAccessUtils.GetWebMediaType(item);
      webMediaItem.Title = (string)item.Aspects[MediaAspect.ASPECT_ID][MediaAspect.ATTR_TITLE];

      return webMediaItem;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}
