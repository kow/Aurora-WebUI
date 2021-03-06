using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using Aurora.Simulation.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.Imaging;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Services.DataService;
using OpenMetaverse.StructuredData;

using System.Collections.Specialized;

using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using BitmapProcessing;

namespace OpenSim.Services
{
    public class WireduxHandler : IService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public IHttpServer m_server = null;
        public IHttpServer m_server2 = null;
        string m_servernick = "hippogrid";
        protected IRegistryCore m_registry;
        public string Name
        {
            get { return GetType().Name; }
        }

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (config.Configs["GridInfoService"] != null)
                m_servernick = config.Configs["GridInfoService"].GetString("gridnick", m_servernick);
            m_registry = registry;
            if (handlerConfig.GetString("WireduxHandler", "") != Name)
                return;
            string Password = handlerConfig.GetString("WireduxHandlerPassword", String.Empty);
            if (Password != "")
            {
                m_server = registry.RequestModuleInterface<ISimulationBase>().GetHttpServer(handlerConfig.GetUInt("WireduxHandlerPort"));
                //This handler allows sims to post CAPS for their sims on the CAPS server.
                m_server.AddStreamHandler(new WireduxHTTPHandler(Password, registry));
                m_server2 = registry.RequestModuleInterface<ISimulationBase>().GetHttpServer(handlerConfig.GetUInt("WireduxTextureServerPort"));
                m_server2.AddHTTPHandler("GridTexture", OnHTTPGetTextureImage);
                m_server2.AddHTTPHandler("MapTexture", OnHTTPGetMapImage);
            }
        }

        public void FinishedStartup()
        {
        }

        public Hashtable OnHTTPGetTextureImage(Hashtable keysvals)
        {
            Hashtable reply = new Hashtable();

            if (keysvals["method"].ToString() != "GridTexture")
                return reply;

            m_log.Debug("[WebUI]: Sending image jpeg");
            int statuscode = 200;
            byte[] jpeg = new byte[0];
            IAssetService m_AssetService = m_registry.RequestModuleInterface<IAssetService>();

            MemoryStream imgstream = new MemoryStream();
            Bitmap mapTexture = new Bitmap(1, 1);
            ManagedImage managedImage;
            Image image = (Image)mapTexture;

            try
            {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                imgstream = new MemoryStream();

                // non-async because we know we have the asset immediately.
                AssetBase mapasset = m_AssetService.Get(keysvals["uuid"].ToString());

                // Decode image to System.Drawing.Image
                if (OpenJPEG.DecodeToImage(mapasset.Data, out managedImage, out image))
                {
                    // Save to bitmap


                    mapTexture = ResizeBitmap(image, 128, 128);
                    EncoderParameters myEncoderParameters = new EncoderParameters();
                    myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);

                    // Save bitmap to stream
                    mapTexture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);



                    // Write the stream to a byte array for output
                    jpeg = imgstream.ToArray();
                }
            }
            catch (Exception)
            {
                // Dummy!
                m_log.Warn("[WebUI]: Unable to post image.");
            }
            finally
            {
                // Reclaim memory, these are unmanaged resources
                // If we encountered an exception, one or more of these will be null
                if (mapTexture != null)
                    mapTexture.Dispose();

                if (image != null)
                    image.Dispose();

                if (imgstream != null)
                {
                    imgstream.Close();
                    imgstream.Dispose();
                }
            }


            reply["str_response_string"] = Convert.ToBase64String(jpeg);
            reply["int_response_code"] = statuscode;
            reply["content_type"] = "image/jpeg";

            return reply;
        }

        public Hashtable OnHTTPGetMapImage(Hashtable keysvals)
        {
            Hashtable reply = new Hashtable();

            if (keysvals["method"].ToString() != "MapTexture")
                return reply;

            int zoom = 20;
            int x = 0;
            int y = 0;

            if (keysvals.ContainsKey("zoom"))
                zoom = int.Parse(keysvals["zoom"].ToString());
            if (keysvals.ContainsKey("x"))
                x = (int)float.Parse(keysvals["x"].ToString());
            if (keysvals.ContainsKey("y"))
                y = (int)float.Parse(keysvals["y"].ToString());

            m_log.Debug("[WebUI]: Sending map image jpeg");
            int statuscode = 200;
            byte[] jpeg = new byte[0];
            
            MemoryStream imgstream = new MemoryStream();
            Bitmap mapTexture = CreateZoomLevel(zoom, x, y);
            EncoderParameters myEncoderParameters = new EncoderParameters();
            myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);

            // Save bitmap to stream
            mapTexture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);

            // Write the stream to a byte array for output
            jpeg = imgstream.ToArray();

            // Reclaim memory, these are unmanaged resources
            // If we encountered an exception, one or more of these will be null
            if (mapTexture != null)
                mapTexture.Dispose();

            if (imgstream != null)
            {
                imgstream.Close();
                imgstream.Dispose();
            }

            reply["str_response_string"] = Convert.ToBase64String(jpeg);
            reply["int_response_code"] = statuscode;
            reply["content_type"] = "image/jpeg";

            return reply;
        }

        public Bitmap ResizeBitmap(Image b, int nWidth, int nHeight)
        {
            Bitmap newsize = new Bitmap(nWidth, nHeight);
            Graphics temp = Graphics.FromImage(newsize);
            temp.DrawImage(b, 0, 0, nWidth, nHeight);
            temp.SmoothingMode = SmoothingMode.AntiAlias;
            temp.DrawString(m_servernick, new Font("Arial", 8, FontStyle.Regular), new SolidBrush(Color.FromArgb(90, 255, 255, 50)), new Point(2, 115));

            return newsize;
        }


        private Bitmap CreateZoomLevel(int zoomLevel, int centerX, int centerY)
        {
            if (!Directory.Exists("MapTiles"))
                Directory.CreateDirectory("MapTiles");

            string fileName = Path.Combine("MapTiles", "Zoom" + zoomLevel + "X" + centerX + "Y" + centerY + ".jpg");
            if (File.Exists(fileName))
            {
                DateTime lastWritten = File.GetLastWriteTime(fileName);
                if ((DateTime.Now - lastWritten).Minutes < 10) //10 min cache
                    return (Bitmap)Bitmap.FromFile(fileName);
            }

            List<GridRegion> regions = m_registry.RequestModuleInterface<IGridService>().GetRegionRange(UUID.Zero,
                    (int)(centerX * (int)Constants.RegionSize - (zoomLevel * (int)Constants.RegionSize)),
                    (int)(centerX * (int)Constants.RegionSize + (zoomLevel * (int)Constants.RegionSize)),
                    (int)(centerY * (int)Constants.RegionSize - (zoomLevel * (int)Constants.RegionSize)),
                    (int)(centerY * (int)Constants.RegionSize + (zoomLevel * (int)Constants.RegionSize)));
            List<Image> bitImages = new List<Image>();
            List<FastBitmap> fastbitImages = new List<FastBitmap>();

            foreach (GridRegion r in regions)
            {
                AssetBase texAsset = m_registry.RequestModuleInterface<IAssetService>().Get(r.TerrainImage.ToString());

                if (texAsset != null)
                {
                    ManagedImage managedImage;
                    Image image;
                    if (OpenJPEG.DecodeToImage(texAsset.Data, out managedImage, out image))
                    {
                        bitImages.Add(image);
                        fastbitImages.Add(new FastBitmap((Bitmap)image));
                    }
                }
            }

            int imageSize = 2560;
            float zoomScale = (imageSize / zoomLevel);
            Bitmap mapTexture = new Bitmap(imageSize, imageSize);
            Graphics g = Graphics.FromImage(mapTexture);
            Color seaColor = Color.FromArgb(29, 71, 95);
            SolidBrush sea = new SolidBrush(seaColor);
            g.FillRectangle(sea, 0, 0, imageSize, imageSize);

            for (int i = 0; i < regions.Count; i++)
            {
                float x = ((regions[i].RegionLocX - (centerX * (float)Constants.RegionSize) + Constants.RegionSize / 2) / (float)Constants.RegionSize);
                float y = ((regions[i].RegionLocY - (centerY * (float)Constants.RegionSize) + Constants.RegionSize / 2) / (float)Constants.RegionSize);

                int regionWidth = regions[i].RegionSizeX / Constants.RegionSize;
                int regionHeight = regions[i].RegionSizeY / Constants.RegionSize;
                float posX = (x * zoomScale) + imageSize / 2;
                float posY = (y * zoomScale) + imageSize / 2;
                g.DrawImage(bitImages[i], posX, imageSize - posY, zoomScale * regionWidth, zoomScale * regionHeight); // y origin is top
            }

            mapTexture.Save(fileName, ImageFormat.Jpeg);

            return mapTexture;
        }

        // From msdn
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
    }

    public class WireduxHTTPHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_password;
        protected IRegistryCore m_registry;

        public WireduxHTTPHandler(string pass, IRegistryCore reg) :
            base("POST", "/WIREDUX")
        {
            m_registry = reg;
            m_password = Util.Md5Hash(pass);

        }

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);
            string method = string.Empty;
            try
            {
                OSDMap map = (OSDMap)OSDParser.DeserializeJson(body);
                //Make sure that the person who is calling can access the web service
                if (VerifyPassword(map))
                {
                    method = map["Method"].AsString();
                    if (method == "Login")
                    {
                        return ProcessLogin(map);
                    }
                    else if (method == "AdminLogin")
                    {
                        return ProcessAdminLogin(map);
                    }
                    else if (method == "CreateAccount")
                    {
                        return ProcessCreateAccount(map);
                    }
                    else if (method == "OnlineStatus")
                    {
                        return ProcessOnlineStatus(map);
                    }
                    else if (method == "Authenticated")
                    {
                        return Authenticated(map);
                    }
                    else if (method == "GetGridUserInfo")
                    {
                        return GetGridUserInfo(map);
                    }
                    else if (method == "ChangePassword")
                    {
                        return ChangePassword(map);
                    }
                    else if (method == "CheckIfUserExists")
                    {
                        return CheckIfUserExists(map);
                    }
                    else if (method == "SaveEmail")
                    {
                        return SaveEmail(map);
                    }
                    else if (method == "ChangeName")
                    {
                        return ChangeName(map);
                    }
                    else if (method == "ConfirmUserEmailName")
                    {
                        return ConfirmUserEmailName(map);
                    }
                    else if (method == "ForgotPassword")
                    {
                        return ForgotPassword(map);
                    }
                    else if (method == "GetProfile")
                    {
                        return GetProfile(map);
                    }
                    else if (method == "GetAvatarArchives")
                    {
                        return GetAvatarArchives(map);
                    }
                    else if (method == "DeleteUser")
                    {
                        return DeleteUser(map);
                    }
                    else if (method == "BanUser")
                    {
                        return BanUser(map);
                    }
                    else if (method == "TempBanUser")
                    {
                        return TempBanUser(map);
                    }
                    else if (method == "UnBanUser")
                    {
                        return UnBanUser(map);
                    }
                    else if (method == "FindUsers")
                    {
                        return FindUsers(map);
                    }
                    else if (method == "GetAbuseReports")
                    {
                        return GetAbuseReports(map);
                    }
                    else if (method == "AbuseReportSaveNotes")
                    {
                        return AbuseReportSaveNotes(map);
                    }
                    else if (method == "AbuseReportMarkComlete")
                    {
                        return AbuseReportMarkComlete(map);
                    }
                }
            }
            catch (Exception)
            {
            }
            OSDMap resp = new OSDMap();
            resp.Add("response", OSD.FromString("Failed"));
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private bool VerifyPassword(OSDMap map)
        {
            if (map.ContainsKey("WebPassword"))
            {
                return map["WebPassword"] == m_password;
            }
            return false;
        }

        private byte[] CheckIfUserExists(OSDMap map)
        {
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = accountService.GetUserAccount(UUID.Zero, map["Name"].AsString());

            bool Verified = user != null;
            OSDMap resp = new OSDMap();
            resp["Verified"] = OSD.FromString(Verified.ToString());
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] ProcessCreateAccount(OSDMap map)
        {
            bool Verified = false;
            string Name = map["Name"].AsString();
            string PasswordHash = map["PasswordHash"].AsString();
            //string PasswordSalt = map["PasswordSalt"].AsString();
            string HomeRegion = map["HomeRegion"].AsString();
            string Email = map["Email"].AsString();
            string AvatarArchive = map["AvatarArchive"].AsString();

  

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            if (accountService == null)
                return null;

            if (!PasswordHash.StartsWith("$1$"))
                PasswordHash = "$1$" + Util.Md5Hash(PasswordHash);
            PasswordHash = PasswordHash.Remove(0, 3); //remove $1$

            accountService.CreateUser(Name, PasswordHash, Email);
            UserAccount user = accountService.GetUserAccount(UUID.Zero, Name);
            IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
            IGridService gridService = m_registry.RequestModuleInterface<IGridService> ();
            if (agentInfoService != null && gridService != null)
            {
                GridRegion r = gridService.GetRegionByName (UUID.Zero, HomeRegion);
                agentInfoService.SetHomePosition (user.PrincipalID.ToString (), r.RegionID, new Vector3 (r.RegionSizeX / 2, r.RegionSizeY / 2, 20), Vector3.Zero);
            }

            Verified = user != null;
            UUID userID = UUID.Zero;

            if (Verified)
            {
                // could not find a way to save this data here.
                /*DateTime RLDOB = map["RLDOB"].AsDate();
                string RLFirstName = map["RLFisrtName"].AsString();
                string RLLastName = map["RLLastName"].AsString();
                string RLAdress = map["RLAdress"].AsString();
                string RLCity = map["RLCity"].AsString();
                string RLZip = map["RLZip"].AsString();
                string RLCountry = map["RLCountry"].AsString();
                string RLIP = map["RLIP"].AsString();*/
                // could not find a way to save this data here.


                userID = user.PrincipalID;
                user.UserLevel = -1;
                
                accountService.StoreUserAccount(user);

                IProfileConnector profileData = DataManager.RequestPlugin<IProfileConnector>();
                IUserProfileInfo profile = profileData.GetUserProfile(user.PrincipalID);
                if (profile == null)
                {
                    profileData.CreateNewProfile(user.PrincipalID);
                    profile = profileData.GetUserProfile(user.PrincipalID);
                }
                if (AvatarArchive.Length > 0)
                    profile.AArchiveName = AvatarArchive + ".database";

                profile.IsNewUser = true;
                profileData.UpdateUserProfile(profile);
            }

            OSDMap resp = new OSDMap();
            resp["Verified"] = OSD.FromBoolean(Verified);
            resp["UUID"] = OSD.FromUUID(userID);
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] ProcessLogin(OSDMap map)
        {
            bool Verified = false;
            string Name = map["Name"].AsString();
            string Password = map["Password"].AsString();

            ILoginService loginService = m_registry.RequestModuleInterface<ILoginService>();
            UUID secureSessionID;
            UUID userID = UUID.Zero;

            LoginResponse loginresp = loginService.VerifyClient(Name, Password, UUID.Zero, false, "", "", "", out secureSessionID);
            //Null means it went through without an error
            Verified = loginresp == null;
            if (Verified)
            {
                userID = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(UUID.Zero, Name).PrincipalID;
            }


            OSDMap resp = new OSDMap();
            resp["Verified"] = OSD.FromBoolean(Verified);
            resp["UUID"] = OSD.FromUUID(userID);
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] ProcessAdminLogin(OSDMap map)
        {
            bool Verified = false;
            string Name = map["Name"].AsString();
            string Password = map["Password"].AsString();

            ILoginService loginService = m_registry.RequestModuleInterface<ILoginService>();
            UUID secureSessionID;
            UUID userID = UUID.Zero;

            LoginResponse loginresp = loginService.VerifyClient(Name, Password, UUID.Zero, false, "", "", "", out secureSessionID);
            //Null means it went through without an error
            Verified = loginresp == null;
            if (Verified)
            {
                UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(UUID.Zero, Name);
                if ((account.UserFlags & 2048) == 2048) //Admin flag
                    userID = account.PrincipalID;
            }


            OSDMap resp = new OSDMap();
            resp["Verified"] = OSD.FromBoolean(Verified);
            resp["UUID"] = OSD.FromUUID(userID);
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] ProcessOnlineStatus(OSDMap map)
        {
            ILoginService loginService = m_registry.RequestModuleInterface<ILoginService>();
            bool LoginEnabled = loginService.MinLoginLevel == 0;

            OSDMap resp = new OSDMap();
            resp["Online"] = OSD.FromInteger(1);
            resp["LoginEnabled"] = OSD.FromInteger(LoginEnabled ? 1 : 0);
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        private byte[] Authenticated(OSDMap map)
        {
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = accountService.GetUserAccount(UUID.Zero, map["UUID"].AsUUID());

            bool Verified = user != null;
            OSDMap resp = new OSDMap();
            resp["Verified"] = OSD.FromBoolean(Verified);

            if (Verified)
            {
                user.UserLevel = 0;
                accountService.StoreUserAccount(user);
            }

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        /// <summary>
        /// Gets user information for change user info page on site
        /// </summary>
        /// <param name="map">UUID</param>
        /// <returns>Verified, HomeName, HomeUUID, Online, Email, FirstName, LastName</returns>
        byte[] GetGridUserInfo(OSDMap map)
        {
            string uuid = String.Empty;
            uuid = map["UUID"].AsString();

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = accountService.GetUserAccount(UUID.Zero, map["UUID"].AsUUID());
            IAgentInfoService agentService = m_registry.RequestModuleInterface<IAgentInfoService>();

            UserInfo userinfo;
            OSDMap resp = new OSDMap();

            bool verified = user != null;
            resp["Verified"] = OSD.FromBoolean(verified);
            if (verified)
            {
                userinfo = agentService.GetUserInfo(uuid);
                IGridService gs = m_registry.RequestModuleInterface<IGridService>();
                Services.Interfaces.GridRegion gr = gs.GetRegionByUUID(UUID.Zero, userinfo.HomeRegionID);

                resp["HomeUUID"] = OSD.FromUUID(userinfo.HomeRegionID);
                resp["HomeName"] = OSD.FromString(gr.RegionName);
                resp["Online"] = OSD.FromBoolean(userinfo.IsOnline);
                resp["Email"] = OSD.FromString(user.Email);
                resp["Name"] = OSD.FromString(user.Name);
            }

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        /// <summary>
        /// After conformation the email is saved
        /// </summary>
        /// <param name="map">UUID, Email</param>
        /// <returns>Verified</returns>
        byte[] SaveEmail(OSDMap map)
        {
            string email = map["Email"].AsString();

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = accountService.GetUserAccount(UUID.Zero, map["UUID"].AsUUID());
            OSDMap resp = new OSDMap();

            bool verified = user != null;
            resp["Verified"] = OSD.FromBoolean(verified);
            if (verified)
            {
                user.Email = email;
                user.UserLevel = 0;
                accountService.StoreUserAccount(user);
            }
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        /// <summary>
        /// Changes user name
        /// </summary>
        /// <param name="map">UUID, FirstName, LastName</param>
        /// <returns>Verified</returns>
        byte[] ChangeName(OSDMap map)
        {
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = accountService.GetUserAccount(UUID.Zero, map["UUID"].AsUUID());
            OSDMap resp = new OSDMap();

            bool verified = user != null;
            resp["Verified"] = OSD.FromBoolean(verified);
            if (verified)
            {
                user.Name = map["Name"].AsString();
                accountService.StoreUserAccount(user);
            }

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] ChangePassword(OSDMap map)
        {
            string Password = map["Password"].AsString();
            string newPassword = map["NewPassword"].AsString();

            ILoginService loginService = m_registry.RequestModuleInterface<ILoginService>();
            UUID secureSessionID;
            UUID userID = map["UUID"].AsUUID();


            IAuthenticationService auths = m_registry.RequestModuleInterface<IAuthenticationService>();

            LoginResponse loginresp = loginService.VerifyClient(userID, Password, UUID.Zero, false, "", "", "", out secureSessionID);
            OSDMap resp = new OSDMap();
            //Null means it went through without an error
            bool Verified = loginresp == null;

            if ((auths.Authenticate(userID, Password, 100) != string.Empty) && (Verified))
            {
                auths.SetPassword(userID, newPassword);
                resp["Verified"] = OSD.FromBoolean(Verified);
            }

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] ForgotPassword(OSDMap map)
        {
            UUID UUDI = map["UUID"].AsUUID();
            string Password = map["Password"].AsString();

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = accountService.GetUserAccount(UUID.Zero, UUDI);

            OSDMap resp = new OSDMap();
            bool verified = user != null;
            resp["Verified"] = OSD.FromBoolean(verified);
            resp["UserLevel"] = OSD.FromInteger(0);
            if (verified)
            {
                resp["UserLevel"] = OSD.FromInteger(user.UserLevel);
                if (user.UserLevel >= 0)
                {
                    IAuthenticationService auths = m_registry.RequestModuleInterface<IAuthenticationService>();
                    auths.SetPassword(user.PrincipalID, Password);
                }
                else
                {
                    resp["Verified"] = OSD.FromBoolean(false);
                }
            }

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] ConfirmUserEmailName(OSDMap map)
        {
            string Name = map["Name"].AsString();
            string Email = map["Email"].AsString();

            OSDMap resp = new OSDMap();
            resp["Verified"] = OSD.FromBoolean(false);
            resp["Error"] = OSD.FromString("");
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            UserAccount user = accountService.GetUserAccount(UUID.Zero, Name);
            bool verified = user != null;

            if (verified)
            {
                if (user.UserLevel >= 0)
                {
                    resp["UUID"] = OSD.FromUUID(user.PrincipalID);
                    if (user.Email.ToLower() == Email.ToLower())
                    {
                        resp["Verified"] = OSD.FromBoolean(true);
                    }
                    else
                    {
                        resp["Error"] = OSD.FromString("Email does not match the user name.");
                    }
                }
                else
                {
                    resp["Error"] = OSD.FromString("This account is disabled.");
                }
            }
            else
            {
                resp["Error"] = OSD.FromString("No such user.");
            }


            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] GetProfile(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            string Name = map["Name"].AsString();

            UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(UUID.Zero, Name);
            if (account != null)
            {
                OSDMap accountMap = new OSDMap();
                accountMap["Created"] = account.Created;
                accountMap["PrincipalID"] = account.PrincipalID;
                TimeSpan diff = DateTime.Now - Util.ToDateTime(account.Created);
                int years = (int)diff.TotalDays / 356;
                int days = years > 0 ? (int)diff.TotalDays / years : (int)diff.TotalDays;
                accountMap["TimeSinceCreated"] = years + " years, " + days + " days";
                IProfileConnector profileConnector = Aurora.DataManager.DataManager.RequestPlugin<IProfileConnector>();
                IUserProfileInfo profile = profileConnector.GetUserProfile(account.PrincipalID);
                if (profile != null)
                {
                    resp["profile"] = profile.ToOSD(false);//not trusted, use false

                    if (account.UserFlags == 0)
                        account.UserFlags = 2; //Set them to no info given
                    string flags = ((IUserProfileInfo.ProfileFlags)account.UserFlags).ToString();
                    IUserProfileInfo.ProfileFlags.NoPaymentInfoOnFile.ToString();

                    accountMap["AccountInfo"] = (profile.CustomType != "" ? profile.CustomType :
                        account.UserFlags == 0 ? "Resident" : "Admin") + "\n" + flags;
                    UserAccount partnerAccount = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(UUID.Zero, profile.Partner);
                    if (partnerAccount != null)
                        accountMap["Partner"] = partnerAccount.Name;
                    else
                        accountMap["Partner"] = "";
                }
                resp["account"] = accountMap;
            }

            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] GetAvatarArchives(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            List<AvatarArchive> temp = DataManager.RequestPlugin<IAvatarArchiverConnector>().GetAvatarArchives(true);

            string names = "";
            string snapshot = "";

            foreach (AvatarArchive a in temp)
            {
                names += a.Name + ",";
                snapshot += a.Snapshot + ",";
            }
            if (names.Length > 0)
            {
                resp["names"] = names.Substring(0, names.Length - 1);
                resp["snapshot"] = snapshot.Substring(0, snapshot.Length - 1);
                resp["Verified"] = OSD.FromBoolean(true);
            }
            else
            {
                resp["Verified"] = OSD.FromBoolean(false);
            }


            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] DeleteUser(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            UUID agentID = map["UserID"].AsUUID();
            IAgentInfo GetAgent = DataManager.RequestPlugin<IAgentConnector>().GetAgent(agentID);

            if (GetAgent == null)
            {
                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
            else
            {
                GetAgent.Flags &= ~IAgentFlags.PermBan;
                DataManager.RequestPlugin<IAgentConnector>().UpdateAgent(GetAgent);

                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
        }

        byte[] BanUser(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            UUID agentID = map["UserID"].AsUUID();
            IAgentInfo GetAgent = DataManager.RequestPlugin<IAgentConnector>().GetAgent(agentID);

            if (GetAgent == null)
            {
                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
            else
            {
                GetAgent.Flags &= ~IAgentFlags.PermBan;
                DataManager.RequestPlugin<IAgentConnector>().UpdateAgent(GetAgent);

                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
        }

        byte[] TempBanUser(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            UUID agentID = map["UserID"].AsUUID();
            IAgentInfo GetAgent = DataManager.RequestPlugin<IAgentConnector>().GetAgent(agentID);

            if (GetAgent == null)
            {
                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
            else
            {
                GetAgent.Flags &= ~IAgentFlags.TempBan;
                GetAgent.OtherAgentInformation["TemperaryBanInfo"] = resp["BannedUntil"];
                DataManager.RequestPlugin<IAgentConnector>().UpdateAgent(GetAgent);

                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
        }

        byte[] UnBanUser(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            UUID agentID = map["UserID"].AsUUID();
            IAgentInfo GetAgent = DataManager.RequestPlugin<IAgentConnector>().GetAgent(agentID);

            if (GetAgent == null)
            {
                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
            else
            {
                GetAgent.Flags &= IAgentFlags.PermBan;
                DataManager.RequestPlugin<IAgentConnector>().UpdateAgent(GetAgent);

                resp["Finished"] = OSD.FromBoolean(true);
                string xmlString = OSDParser.SerializeJsonString(resp);
                UTF8Encoding encoding = new UTF8Encoding();
                return encoding.GetBytes(xmlString);
            }
        }

        byte[] FindUsers(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            int start = map["Start"].AsInteger();
            int end = map["End"].AsInteger();
            string Query = map["Query"].AsString();
            List<UserAccount> accounts = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccounts(UUID.Zero, Query);

            OSDArray users = new OSDArray();
            for(int i = start; i < end && i < accounts.Count; i++)
            {
                UserAccount acc = accounts[i];
                OSDMap userInfo = new OSDMap();
                userInfo["PrincipalID"] = acc.PrincipalID;
                userInfo["UserName"] = acc.Name;
                userInfo["Created"] = acc.Created;
                userInfo["UserFlags"] = acc.UserFlags;
                users.Add(userInfo);
            }
            resp["Users"] = users;

            resp["Finished"] = OSD.FromBoolean(true);
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] GetAbuseReports(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            IAbuseReports ar = m_registry.RequestModuleInterface<IAbuseReports>();

            List<AbuseReport> lar = ar.GetAbuseReports(map["start"].AsInteger(), map["count"].AsInteger(), map["filter"].AsString());
            OSDArray returnvalue = new OSDArray();
            foreach (AbuseReport tar in lar)
            {
                returnvalue.Add(tar.ToOSD());
            }
            resp["abusereports"] = returnvalue;
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] AbuseReportMarkComlete(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            IAbuseReports ar = m_registry.RequestModuleInterface<IAbuseReports>();
            AbuseReport tar = ar.GetAbuseReport(map["Number"].AsInteger(), map["WebPassword"].AsString());
            tar.Active = false;
            ar.UpdateAbuseReport(tar, map["WebPassword"].AsString());
            resp["Finished"] = OSD.FromBoolean(true);
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] AbuseReportSaveNotes(OSDMap map)
        {
            OSDMap resp = new OSDMap();
            IAbuseReports ar = m_registry.RequestModuleInterface<IAbuseReports>();
            AbuseReport tar = ar.GetAbuseReport(map["Number"].AsInteger(), map["WebPassword"].AsString());
            tar.Notes = map["Notes"].ToString();
            ar.UpdateAbuseReport(tar, map["WebPassword"].AsString());
            resp["Finished"] = OSD.FromBoolean(true);
            string xmlString = OSDParser.SerializeJsonString(resp);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }
    }
}