using Panacea.Core;
using Panacea.Controls;
using Panacea.Modularity;
using Panacea.Modularity.Citrix;
using Panacea.Modularity.Imprivata;
using Panacea.Modularity.RemoteDesktop;
using Panacea.Modularity.RfidReader;
using Panacea.Modularity.UiManager;
using Panacea.Modularity.WebBrowsing;
using Panacea.Modules.CardActions.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Panacea.Modules.CardActions.ViewModels;
using System.Timers;

namespace Panacea.Modules.CardActions
{
    class CardActionsController : IPlugin
    {
        private readonly PanaceaServices _core;
        private GetActionsResponse _obj;
        private List<CardAction> _actions;
        private int _currentAction = 0;
        private string _currentCardCode;
        private string _lastCard;
        private AuthenticationResult authResult;

        private void tryGetAnything<T>(Action<T> cb, Action errorCB = null) where T : IPlugin
        {
            T _plug = _core.PluginLoader.GetPlugin<T>();
            if (_plug != null)
            {
                cb(_plug);
                return;
            } else
            {
                if (errorCB != null)
                {
                    errorCB();
                    return;
                } else
                {
                    _core.Logger.Warn(this, "not Loaded");
                }
            }
        }
        private void tryGetCitrix(Action<ICitrixPlugin> cb, Action errorCB = null)
        {
            if (_core.TryGetCitrix(out ICitrixPlugin _citrix))
            {
                cb(_citrix);
                return;
            }
            else
            {
                if (errorCB != null)
                {
                    errorCB();
                    return;
                } else
                {
                    _core.Logger.Error(this, "citrix not loaded");
                }
            }
        }
        private void tryGetRDC(Action<IRemoteDesktop> cb, Action errorCB = null)
        {
            if (_core.TryGetGetRemoteDesktopPlugin(out IRemoteDesktop _rdc))
            {
                cb(_rdc);
                return;
            }
            else
            {
                if (errorCB != null)
                {
                    errorCB();
                    return;
                }
                else
                {
                    _core.Logger.Error(this, "remote resktop not loaded");
                }
            }
        }
        private void tryGetImprivata(Action<IImprivataPlugin> cb, Action errorCB=null)
        {
            if (_core.TryGetImprivata(out IImprivataPlugin _imprivata))
            {
                cb(_imprivata);
                return;
            }
            else
            {
                if (errorCB != null)
                {
                    errorCB();
                    return;
                }
                else
                {
                    _core.Logger.Error(this, "citrix not loaded");
                }
            }
        }
        private async Task tryGetUiManager(Func<IUiManager, Task> cb, Action errorCB = null)
        {
            if (_core.TryGetUiManager(out IUiManager _ui))
            {
                await cb(_ui);
                return;
            }
            else
            {
                if (errorCB != null)
                {
                    errorCB();
                    return;
                }
                else
                {
                    _core.Logger.Error(this, "ui manager not loaded");
                }
            }
        }

        public CardActionsController(PanaceaServices core)
        {
            _core = core;
        }
        public Task BeginInit()
        {
            return Task.CompletedTask;            
        }
        public Task EndInit()
        {
            RfidReaderPluginContainer r = _core.GetRfidReaderContainer();
            r.CardConnected += RFIDReaders_CardConnected;
            return Task.CompletedTask;
        }

        private void R_CardDisconnected(object sender, string e)
        {
            return;
        }

        public void Dispose()
        {
            return;
        }


        public Task Shutdown()
        {
            return Task.CompletedTask;
        }

        int _idleTime = 160;
        Process _vmwareProcess;
        Timer _idleTimer;

        private async void RFIDReaders_CardConnected(object sender, string e)
        {
            if (string.IsNullOrEmpty(e)) return;
            if (_core.TryGetUiManager(out IUiManager _uiManager))
            {
                await _uiManager.DoWhileBusy(async () =>
                {
                    _currentCardCode = e;
                    try
                    {
                        var response = await _core.HttpClient.GetObjectAsync<GetActionsResponse>("card_actions/get_cardcategories/" + e + "/");
                        if (response.Success)
                        {
                            _obj = response.Result;
                            _actions = _obj.CardCategoriesObject.CardActions.CardCategories
                                .SelectMany(cc => cc.Actions.Select(a => a.Action))
                                .ToList()
                                .Where(ac => ac.ForSignedInUser == (_obj.User != null && _core.UserService.User.Id == _obj.User.Id))
                                .ToList();
                            _currentAction = 0;

                        }
                    }
                    catch
                    {
                    }
                });
            }
            else
            {
                _core.Logger.Error(this, "ui manager not loaded");
            }
            await StartActions();
            _lastCard = e;
        }

        private async Task StartActions() {
            //TODO: WHAT IS THE REAL PURPOSE OF THIS HUGE TRY-CATCH?
            try
            {
                if (_core.TryGetCitrix(out ICitrixPlugin _citrix))
                {
                    if (_citrix.IsRunning() && _actions.Any(a => a.Action.Contains("citrix")))
                    {
                        _citrix.Stop();
                        if (_currentCardCode == _lastCard) return;
                    }
                }
                else
                {
                    _core.Logger.Error(this, "citrix not loaded");
                }
                if (_core.TryGetGetRemoteDesktopPlugin(out IRemoteDesktop _remote))
                {
                    if (_remote.IsRunning() && _actions.Any(a => a.Action.Contains("rdc")))
                    {
                        _remote.Disconnect();
                    }
                }
                else
                {
                    _core.Logger.Error(this, "rdc not loaded");
                }
                if (_currentAction >= _actions.Count) return;
                CardAction act = _actions[_currentAction];
                _currentAction++;
                await Application.Current.Dispatcher.Invoke(async () => {
                    //TODO: Why do some cases not rerun StartActions?
                    switch (act.Action)
                    {
                        case "launch":
                            launch(act);
                            break;
                        case "open_plugin":
                            openPlugin(act);
                            break;
                        case "login_user":
                            await loginUserAsync();
                            break;
                        case "logout_user":
                            await logoutUserAsync();
                            break;
                        case "web":
                            await openWebAsync(act);
                            break;
                        case "image":
                            openImage(act);
                            break;
                        case "imprivata":
                            await imprivata(act);
                            break;
                        case "citrix":
                            citrix(act);
                            break;
                        case "imprivata_citrix":
                            await imprivataCitrix(act);
                            break;
                        case "imprivata_uri":
                            await imprivataUri(act);
                            break;
                        case "imprivata_program":
                            await imprivataProgram(act);
                            break;
                        case "rdc":
                            await rdc(act);
                            break;
                        case "imprivata_rdc":
                            await imprivataRDC(act);
                            break;
                    }
                });
            }
            catch (Exception e)//TODO: WHAT IS THE REAL PURPOSE OF THIS HUGE TRY-CATCH?
            {
                _core.Logger.Error(this, e.StackTrace);
            }
            return;
        }
        private async Task imprivataRDC(CardAction act)
        {
            if(_core.TryGetImprivata(out IImprivataPlugin _imprivata))
            {
                try
                {
                    AuthenticationResult result = await _imprivata.AuthenticateCard(_currentCardCode, act.Settings["ImprivataServer"]);
                    authResult = result;
                }
                catch (AuthenticationException e)
                {
                    _core.Logger.Error(this, e.StackTrace);
                }
            } else
            {
                _core.Logger.Error(this, "imprivata not loaded");
            }
        }
        private async Task rdc(CardAction act) {
            if(_core.TryGetGetRemoteDesktopPlugin(out IRemoteDesktop _rdc))
            {
                string username = act.Settings.ContainsKey("username") ? act.Settings["username"] : authResult.Username != null ? authResult.Username : "";
                string password = act.Settings.ContainsKey("password") ? act.Settings["password"] : authResult.Password != null ? authResult.Password : "";
                _rdc.Connect(username, password, "", act.Settings["ip"]);
                await StartActions();
            }
            else
            {
                _core.Logger.Error(this, "rdc not loaded");
            }
        }
        private async Task imprivataProgram(CardAction act)
        {
            if (_vmwareProcess == null)
            {
                if(_core.TryGetImprivata(out IImprivataPlugin _imprivata)) {
                    try
                    {
                        AuthenticationResult result = await _imprivata.AuthenticateCard(_currentCardCode, act.Settings["ImprivataServers"]);
                        authResult = result;
                        try
                        {
                            var info = new ProcessStartInfo(act.Settings["File"] as string)
                            {
                                Arguments = act.Settings["Args"]?.Replace("%username%", result.Username)
                                ?.Replace("%password%", result.Password)
                                ?.Replace("%domain%", result.Domain)
                            };
                            _vmwareProcess = new Process()
                            {
                                StartInfo = info,
                                EnableRaisingEvents = true
                            };
                            _vmwareProcess.Exited += (proc, pargs) =>
                            {
                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    _vmwareProcess.Dispose();
                                    if (_core.TryGetUiManager(out IUiManager _uiManager))
                                    {
                                        _uiManager.Resume();
                                    }
                                    else
                                    {
                                        _core.Logger.Error(this, "ui manager not loaded");
                                    }
                                    _vmwareProcess = null;
                                }));
                            };

                            if (_idleTimer != null)
                            {
                                _idleTimer.Stop();
                                _idleTimer.Dispose();
                            }

                            _idleTimer = new Timer()
                            {
                                Interval = 1000
                            };

                            _idleTimer.Elapsed += _idleTimer_Elapsed;
                            _idleTime = act.Settings.ContainsKey("IdleSeconds") ? int.Parse(act.Settings["IdleSeconds"].ToString()) * 1000 : 15000;
                            _idleTimer.Start();
                            _vmwareProcess.Start();
                            if (_core.TryGetUiManager(out IUiManager _ui))
                            {
                                _ui.Pause();
                            }
                            else
                            {
                                _core.Logger.Error(this, "ui manager not loaded");
                            }
                        }
                        catch (Exception ex)
                        {
                            _core.Logger.Error(this, ex.StackTrace);
                            if (_core.TryGetUiManager(out IUiManager _ui))
                            {
                                _ui.Resume();
                            }
                            else
                            {
                                _core.Logger.Error(this, "ui manager not loaded");
                            }
                            _idleTimer?.Stop();
                            _idleTimer?.Dispose();
                            _idleTimer = null;
                            _vmwareProcess?.Kill();
                            _vmwareProcess?.Dispose();
                            _vmwareProcess = null;
                        }
                    }
                    catch (AuthenticationException e)
                    {
                        _core.Logger.Error(this, e.StackTrace);
                    }
                }
                else
                {
                    _core.Logger.Error(this, "imprivata not loaded");
                }
            }
            else
            {

                _core.GetUiManager().Resume();
                //_window.Resume();
                _idleTimer?.Stop();
                _idleTimer?.Dispose();
                _idleTimer = null;
                _vmwareProcess?.Kill();
                _vmwareProcess?.Dispose();
                _vmwareProcess = null;
            }
        }
        private void _idleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var time = Win32.GetIdleTime();
            if (time > _idleTime)
            {
                _idleTimer.Stop();
                _idleTimer?.Dispose();
                _vmwareProcess?.Kill();
                _vmwareProcess?.Dispose();
                _vmwareProcess = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_core.TryGetUiManager(out IUiManager _ui))
                    {
                        _ui.Resume();
                    }
                    else
                    {
                        _core.Logger.Error(this, "ui manager not loaded");
                    }
                });
            }
        }
        private async Task imprivataUri(CardAction act) {
            string uri = act.Settings["Uri"];
            if(_core.TryGetImprivata(out IImprivataPlugin _imprivata))
            {
                try
                {
                    AuthenticationResult result = await _imprivata.AuthenticateCard(_currentCardCode, act.Settings["ImprivataServers"]);
                    var uriReplaced = uri
                                    .Replace("%username%", result.Username)
                                    .Replace("%password%", result.Password)
                                    .Replace("%domain%", result.Domain);
                    //TODO: UNDOABLE IN CURRENT API! _comm.OpenUri(uriReplaced);
                }
                catch (AuthenticationException e)
                {
                    _core.Logger.Error(this, e.StackTrace);
                }
            }
            else
            {
                _core.Logger.Error(this, "imprivata not loaded");
            }
        }
        private async Task imprivataCitrix(CardAction act)
        {
            if(_core.TryGetCitrix(out ICitrixPlugin _citrix))
            {
                if (_citrix.IsRunning())
                {
                    _citrix.Stop();
                }
                else
                {
                    if(_core.TryGetImprivata(out IImprivataPlugin _imprivata)){
                        try
                        {
                            AuthenticationResult result = await _imprivata.AuthenticateCard(_currentCardCode, act.Settings["ImprivataServers"]);
                            authResult = result;
                            string application = ""; // WHERE CAN I GET THIS?
                            _citrix.Start(result.Username, result.Password, result.Domain, act.Settings["CitrixServer"], application);
                        }
                        catch (AuthenticationException e)
                        {
                            _core.Logger.Error(this, "Imprivata auth error: " + e.StackTrace);
                        }
                    }
                }
            }
            else
            {
                _core.Logger.Error(this, "citrix not loaded");
            }
        }
        private void citrix(CardAction act)
        {//WHAT IS THE PURPOSE OF THIS ACTION?!
            if(_core.TryGetCitrix(out ICitrixPlugin _citrix))
            {
                //TODO: Should i get those from authResult???
                string username = "";
                string password = "";
                string domain = "";
                string application = "";
                _citrix.Start(username, password, domain, act.Settings["CitrixServer"], application);
            }
            else
            {
                _core.Logger.Error(this, "citrix not loaded");
            }
        }
        private async Task imprivata(CardAction act)
        {
            if(_core.TryGetImprivata(out IImprivataPlugin _imprivata))
            {
                try
                {
                    AuthenticationResult result = await _imprivata.AuthenticateCard(_currentCardCode, act.Settings["ImprivataServers"]);
                    authResult = result;
                    await StartActions();
                }
                catch (AuthenticationException e)
                {
                    _core.Logger.Error(this, "Imprivata auth error: " + e.StackTrace);
                }
            }
            else
            {
                _core.Logger.Error(this, "imprivata not loaded");
            }
        }
        private void openImage(CardAction act)
        {
            if(_core.TryGetUiManager(out IUiManager _uiManager)){
                ImageViewModel image = new ImageViewModel(act.Settings["url"]);
                _uiManager.Navigate(image);
            }
            else
            {
                _core.Logger.Error(this, "ui manager not loaded");
            }
        }
        private async Task openWebAsync(CardAction act)
        {
            if(_core.TryGetWebBrowser(out IWebBrowserPlugin _webBrowser))
            {
                _webBrowser.OpenUrl(act.Settings["url"]);
            }
            else
            {
                _core.Logger.Error(this, "web browser not loaded");
            }
            await StartActions();
        }
        async private Task logoutUserAsync()
        {
            if (_core.UserService.User.Id != null)
            {
                await _core.UserService.LogoutAsync();
            }
        }
        async private Task loginUserAsync()
        {
            if(_core.UserService.User.Id != null)
            {
                await _core.UserService.LogoutAsync();
            }
            await _core.UserService.LoginAsync(_currentCardCode);
            await StartActions();
            //TODO: IMPLEMENT THIS IN USERSERVICE: await _core.UserService.LoginAsync(_currentCardCode, async () => { await StartActions(); }, null);
        }
        private void openPlugin(CardAction act)
        {
            try
            {
                (_core.PluginLoader.LoadedPlugins[act.Settings["plugin"]] as ICallablePlugin).Call();
            }
            catch (Exception ex)
            {
                _core.Logger.Debug(this.GetType().Name, "Could not start process: " + ex.Message);
            }
        }
        private void launch(CardAction act)
        {
            if (!act.Settings.ContainsKey("file")) return;
            var file = act.Settings["file"];
            string args = "";
            if (act.Settings.ContainsKey("args")) args = act.Settings["args"];
            try
            {
                if (
                    Process.GetProcesses()
                        .Any(p => p.Modules.Cast<ProcessModule>().Any(m => m.FileName == file)))
                {
                    Process.GetProcesses()
                        .Where(p => p.Modules.Cast<ProcessModule>().Any(m => m.FileName == file))
                        .ToList()
                        .ForEach(p =>
                        {
                            p.Kill();
                        });
                    if (_core.TryGetUiManager(out IUiManager _uiManager)){
                        _uiManager.HideKeyboard();
                        _uiManager.EnableFullscreen();
                        return;
                    }
                    else
                    {
                        _core.Logger.Error(this, "ui manager not loaded");
                    }
                }
            }
            catch (Exception ex)
            {
                _core.Logger.Debug(this.GetType().Name, "Could not kill process: " + ex.Message);
            }
            try
            {
                _core.Logger.Debug(this.GetType().Name, file);
                var startinfo = new ProcessStartInfo
                {
                    FileName = file,
                    WorkingDirectory = Path.GetDirectoryName(file),
                    Arguments = args

                };
                var proc = new Process();
                proc.StartInfo = startinfo;
                proc.EnableRaisingEvents = true;
                proc.Exited += (oo, ee) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_core.TryGetUiManager(out IUiManager _uiManager))
                        {
                            _uiManager.HideKeyboard();
                            _uiManager.EnableFullscreen();
                            proc.Dispose();
                        }
                        else
                        {
                            _core.Logger.Error(this, "ui manager not loaded");
                        }
                    }));
                };
                proc.Start();
                if (_core.TryGetUiManager(out IUiManager _ui))
                {
                    _ui.ShowKeyboard();
                    _ui.DisableFullscreen();
                    proc.Dispose();
                }
                else
                {
                    _core.Logger.Error(this, "ui manager not loaded");
                }
            }
            catch (Exception ex)
            {
                _core.Logger.Debug(this.GetType().Name, "Could not start process: " + ex.Message);
            }
        }
    }
}
