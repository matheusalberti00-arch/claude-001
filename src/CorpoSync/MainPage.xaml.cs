using System.Globalization;
using Microsoft.Maui.Storage;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace CorpoSync;

public partial class MainPage : ContentPage
{
	const int CODIGO_CONSENTIMENTO = 0x1717;

	const string UCP = "2a9f";
	const string PESO = "2a9d";
	const string COMP = "2a9c";
	const string SEXO_CH = "2a8c";
	const string NASC_CH = "2a85";
	const string ALT_CH = "2a8e";
	const string RELOGIO = "2a2b"; // Current Time (relógio da balança)

	readonly IBluetoothLE _ble;
	readonly IAdapter _adapter;
	readonly HashSet<string> _seen = new();
	readonly Dictionary<string, ICharacteristic> _canais = new();

	List<Perfil> _perfis = new();
	Perfil? _ativo;
	bool _scanning;
	bool _temPeso;
	DateTime? _melhorQuando;
	TaskCompletionSource<byte[]>? _respostaUcp;
	Medicao _medicao = new();
	readonly GarminService _garmin = new();
	bool _enviando;
	bool _pesagemRecebida;
	bool _pesoAgora;               // já chegou o PESO com carimbo de agora
	DateTime _quandoPesoAgora;     // quando esse peso de agora chegou (p/ esperar a composição)

	public MainPage()
	{
		InitializeComponent();
		_ble = CrossBluetoothLE.Current;
		_adapter = CrossBluetoothLE.Current.Adapter;
		_adapter.ScanTimeout = 30000;
		_adapter.DeviceDiscovered += OnDeviceDiscovered;
		_adapter.DeviceConnectionLost += (s, a) => Log("⚠ Conexão com a balança caiu.");
		_adapter.DeviceDisconnected += (s, a) => Log("Balança desconectou.");
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		CarregarPerfis();
	}

	void CarregarPerfis()
	{
		_perfis = PerfilStore.Carregar();
		PerfilPicker.ItemsSource = _perfis.Select(p => p.Nome).ToList();

		var ativoId = PerfilStore.AtivoId;
		var idx = _perfis.FindIndex(p => p.Id == ativoId);
		if (idx < 0 && _perfis.Count > 0) idx = 0;
		PerfilPicker.SelectedIndex = idx;
		_ativo = idx >= 0 ? _perfis[idx] : null;
	}

	void OnPerfilChanged(object sender, EventArgs e)
	{
		var i = PerfilPicker.SelectedIndex;
		if (i >= 0 && i < _perfis.Count)
		{
			_ativo = _perfis[i];
			PerfilStore.AtivoId = _ativo.Id;
		}
	}

	async void OnPerfisClicked(object sender, EventArgs e)
	{
		await Navigation.PushAsync(new PerfisPage());
	}

	void OnToggleLog(object sender, EventArgs e)
		=> LogCard.IsVisible = !LogCard.IsVisible;

	void Log(string msg)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			LogLabel.Text += $"{DateTime.Now:HH:mm:ss}  {msg}\n";
			await LogScroll.ScrollToAsync(LogLabel, ScrollToPosition.End, false);
		});
	}

	static string Hex(byte[]? b) => (b == null || b.Length == 0) ? "(vazio)" : BitConverter.ToString(b);
	static string Curto(Guid g) { var s = g.ToString(); return s.Length >= 8 ? s.Substring(4, 4) : s; }

	async void OnPesarClicked(object sender, EventArgs e)
	{
		if (_scanning) return;

		if (_ativo == null)
		{
			await DisplayAlert("Perfil", "Crie um perfil primeiro (botão \"Perfis\").", "OK");
			return;
		}

		LogLabel.Text = string.Empty;
		_seen.Clear();
		_canais.Clear();
		_medicao = new Medicao();
		_temPeso = false;
		_melhorQuando = null;
		_pesagemRecebida = false;
		_pesoAgora = false;
		ResultadoCard.IsVisible = false;
		MostrarEnvio(string.Empty);
		Grade.Children.Clear();
		Grade.RowDefinitions.Clear();

		if (!await EnsurePermissionsAsync())
		{
			StatusLabel.Text = "Permissão de Bluetooth negada.";
			Log("Permissão negada. Autorize o Bluetooth nas configurações do app.");
			return;
		}

		if (!_ble.IsOn)
		{
			StatusLabel.Text = "Bluetooth desligado.";
			Log("O Bluetooth do celular está desligado. Ligue e tente de novo.");
			return;
		}

		_scanning = true;
		StatusLabel.Text = "Procurando a balança...";
		Log($"Perfil: {_ativo.Nome}. Procurando a balança...");

		try { await _adapter.StartScanningForDevicesAsync(); }
		catch (Exception ex) { Log("Erro na busca: " + ex.Message); }

		_scanning = false;
	}

	void OnDeviceDiscovered(object? sender, DeviceEventArgs a)
	{
		var d = a.Device;
		if (!_seen.Add(d.Id.ToString())) return;

		var nome = (d.Name ?? string.Empty).ToLowerInvariant();
		if (nome.Contains("bf") || nome.Contains("beurer") || nome.Contains("scale") || nome.Contains("balan"))
		{
			Log($"Achei a balança: {d.Name}. Conectando...");
			_ = ConectarEIdentificarAsync(d);
		}
	}

	async Task ConectarEIdentificarAsync(IDevice device)
	{
		if (_ativo == null) return;
		try
		{
			await _adapter.StopScanningForDevicesAsync();
			bool ok = await ConectarEPrepararAsync(device);
			if (!ok) return;

			MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = "Conectado. SUBA AGORA e fique parado na balança.");
			Log("Conectado. Suba agora; vou ler até chegar na pesagem de agora.");
			_ = LerPesagemAsync(device);
		}
		catch (Exception ex)
		{
			Log("Erro ao conectar/identificar: " + ex.Message);
		}
	}

	// Conecta, (re)descobre os canais, assina as notificações e faz o handshake
	// (desbloquear/registrar o usuário + acertar o relógio). Usada tanto na 1ª
	// conexão quanto nas reconexões que forçam a sincronização.
	async Task<bool> ConectarEPrepararAsync(IDevice device)
	{
		if (_ativo == null) return false;

		await _adapter.ConnectToDeviceAsync(device);
		Log("Conectado. Preparando os canais...");

		_canais.Clear();
		var services = await device.GetServicesAsync();
		foreach (var s in services)
			foreach (var c in await s.GetCharacteristicsAsync())
				_canais[Curto(c.Id)] = c;

		await OuvirAsync(UCP, OnUcpAtualizado);
		await OuvirAsync(PESO, OnMedicaoRecebida);
		await OuvirAsync(COMP, OnMedicaoRecebida);

		byte cLo = (byte)(CODIGO_CONSENTIMENTO & 0xFF);
		byte cHi = (byte)((CODIGO_CONSENTIMENTO >> 8) & 0xFF);

		// NÃO apagamos usuário (isso causava "DEL"). Reaproveitamos o salvo
		// (consentindo) e só criamos um novo se não houver.
		int idx = _ativo.ScaleUserIndex;
		bool pronto = false;
		bool registrouNovo = false;

		if (idx >= 0)
		{
			Log($"Usando seu usuário salvo (nº {idx}). Desbloqueando...");
			var rc = await EscreverUcpEsperar(new byte[] { 0x02, (byte)idx, cLo, cHi }, "Consentir usuário");
			pronto = rc != null && rc.Length >= 3 && rc[0] == 0x20 && rc[2] == 0x01;
			if (!pronto) Log("Não deu para desbloquear o usuário salvo (talvez apagado). Vou criar de novo.");
		}

		if (!pronto)
		{
			var r = await EscreverUcpEsperar(new byte[] { 0x01, cLo, cHi }, "Registrar novo usuário");
			if (r != null && r.Length >= 4 && r[0] == 0x20 && r[2] == 0x01)
			{
				idx = r[3];
				SalvarIndiceNoPerfil(idx);
				pronto = true;
				registrouNovo = true;
				Log($"✔ Usuário criado na balança (nº {idx}).");
			}
			else
			{
				Log("✖ Não consegui registrar o usuário na balança.");
			}
		}

		if (!pronto) return false;

		// Só grava sexo/nascimento/altura na PRIMEIRA vez (ao registrar).
		if (registrouNovo)
		{
			await EscreverPerfil(SEXO_CH, new byte[] { (byte)_ativo.Sexo }, "sexo");
			await EscreverPerfil(NASC_CH, new byte[]
			{
				(byte)(_ativo.AnoNasc & 0xFF), (byte)((_ativo.AnoNasc >> 8) & 0xFF),
				(byte)_ativo.MesNasc, (byte)_ativo.DiaNasc
			}, "nascimento");
			await EscreverPerfil(ALT_CH, new byte[]
			{
				(byte)(_ativo.AlturaCm & 0xFF), (byte)((_ativo.AlturaCm >> 8) & 0xFF)
			}, "altura");
		}

		await AjustarRelogioAsync();
		return true;
	}

	// A balança "desfila" as pesagens guardadas ao conectar, das antigas para as
	// novas. Ficamos conectados enquanto você sobe e mede, sempre guardando a de
	// DATA MAIS NOVA, e encerramos assim que chega a pesagem de agora (ou após ~1 min).
	async Task LerPesagemAsync(IDevice device)
	{
		for (int i = 0; i < 60 && !_pesagemRecebida; i++)
		{
			await Task.Delay(1000);
			if (_pesagemRecebida) break;
			// Se o PESO de agora já chegou mas a composição demorou > 5s, encerra assim mesmo.
			if (_pesoAgora && (DateTime.Now - _quandoPesoAgora).TotalSeconds >= 5)
			{
				Log("(composição não chegou a tempo; encerrando com o que veio)");
				break;
			}
			try { if (_canais.TryGetValue("2a19", out var bat) && bat.CanRead) await bat.ReadAsync(); }
			catch { Log("(conexão caiu durante a leitura)"); break; }
		}

		try { await _adapter.DisconnectDeviceAsync(device); } catch { }

		if (_temPeso)
		{
			bool recente = _melhorQuando.HasValue && Math.Abs((DateTime.Now - _melhorQuando.Value).TotalMinutes) < 3;
			MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = recente
				? "Pesagem de agora recebida! Confira e envie pro Garmin."
				: "Só veio a mais recente guardada. Suba, espere a balança terminar e toque em Pesar de novo.");
		}
		else
		{
			MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = "Não recebi pesagem. Toque em Pesar e suba na balança logo em seguida.");
		}
	}

	void SalvarIndiceNoPerfil(int idx)
	{
		if (_ativo == null) return;
		_ativo.ScaleUserIndex = idx;
		var lista = PerfilStore.Carregar();
		var p = lista.FirstOrDefault(x => x.Id == _ativo.Id);
		if (p != null) { p.ScaleUserIndex = idx; PerfilStore.Salvar(lista); }
	}

	async Task OuvirAsync(string uuid, EventHandler<CharacteristicUpdatedEventArgs> handler)
	{
		if (!_canais.TryGetValue(uuid, out var c)) { Log($"(canal {uuid} não encontrado)"); return; }
		try { c.ValueUpdated += handler; await c.StartUpdatesAsync(); }
		catch (Exception ex) { Log($"(não deu p/ ouvir {uuid}: {ex.Message})"); }
	}

	async Task<byte[]?> EscreverUcpEsperar(byte[] cmd, string desc)
	{
		if (!_canais.TryGetValue(UCP, out var ucp)) { Log("Canal 2a9f não achado."); return null; }
		_respostaUcp = new TaskCompletionSource<byte[]>();
		Log($"→ {desc}: {Hex(cmd)}");
		try { ucp.WriteType = CharacteristicWriteType.WithResponse; await ucp.WriteAsync(cmd); }
		catch (Exception ex) { Log($"   erro ao escrever ({desc}): {ex.Message}"); return null; }

		var done = await Task.WhenAny(_respostaUcp.Task, Task.Delay(8000));
		return done == _respostaUcp.Task ? _respostaUcp.Task.Result : null;
	}

	// Ajusta o relógio da balança (Current Time, 2a2b) para o horário atual.
	async Task AjustarRelogioAsync()
	{
		if (!_canais.TryGetValue(RELOGIO, out var c)) { Log("(relógio 2a2b não achado)"); return; }
		var n = DateTime.Now;
		int dow = (int)n.DayOfWeek;            // 0=domingo..6=sábado (.NET)
		byte bleDow = (byte)(dow == 0 ? 7 : dow); // 1=segunda..7=domingo (Bluetooth)
		var b = new byte[]
		{
			(byte)(n.Year & 0xFF), (byte)((n.Year >> 8) & 0xFF),
			(byte)n.Month, (byte)n.Day, (byte)n.Hour, (byte)n.Minute, (byte)n.Second,
			bleDow, 0x00, 0x00
		};
		try { c.WriteType = CharacteristicWriteType.WithResponse; await c.WriteAsync(b); Log($"Relógio ajustado p/ agora: {Hex(b)}"); }
		catch (Exception ex) { Log($"(não deu p/ ajustar o relógio: {ex.Message})"); }
	}

	async Task EscreverPerfil(string uuid, byte[] valor, string nome)
	{
		if (!_canais.TryGetValue(uuid, out var c)) { Log($"(canal de {nome} não achado)"); return; }
		try { c.WriteType = CharacteristicWriteType.WithResponse; await c.WriteAsync(valor); Log($"Perfil: {nome} = {Hex(valor)}"); }
		catch (Exception ex) { Log($"(não deu p/ gravar {nome}: {ex.Message})"); }
	}

	void OnUcpAtualizado(object? sender, CharacteristicUpdatedEventArgs args)
	{
		var bytes = args.Characteristic.Value ?? Array.Empty<byte>();
		Log($"Resposta 2a9f: {Hex(bytes)}");
		_respostaUcp?.TrySetResult(bytes);
	}

	void OnMedicaoRecebida(object? sender, CharacteristicUpdatedEventArgs args)
	{
		var canal = Curto(args.Characteristic.Id);
		var bytes = args.Characteristic.Value ?? Array.Empty<byte>();
		Log($"DADO {canal}: {Hex(bytes)}");

		if (canal == PESO)
		{
			// A balança despeja as pesagens guardadas ao conectar. Ficamos sempre
			// com a de DATA MAIS NOVA (maior carimbo de hora).
			var tmp = new Medicao();
			tmp.LerPeso(bytes);

			bool maisRecente = !_melhorQuando.HasValue || !tmp.Quando.HasValue
				|| tmp.Quando.Value >= _melhorQuando.Value;

			if (maisRecente)
			{
				_medicao = new Medicao();
				_medicao.LerPeso(bytes);
				_melhorQuando = tmp.Quando;
				_temPeso = true;
				if (_ativo != null) _medicao.CalcularImc(_ativo.AlturaCm);
				MainThread.BeginInvokeOnMainThread(MostrarResultado);
			}

			// Chegou o PESO de agora. NÃO encerramos ainda: a composição (gordura,
			// água, músculo) vem num 2º pacote (2a9c) logo depois. Marcamos e esperamos.
			if (tmp.Quando.HasValue && Math.Abs((DateTime.Now - tmp.Quando.Value).TotalMinutes) < 3)
			{
				if (!_pesoAgora) { _pesoAgora = true; _quandoPesoAgora = DateTime.Now; Log("✔ Peso de agora recebido. Aguardando a composição..."); }
				// Se a composição já veio antes (raro), pode encerrar.
				if (_medicao.GorduraPct.HasValue) _pesagemRecebida = true;
			}
		}
		else if (canal == COMP && _temPeso)
		{
			_medicao.LerComposicao(bytes);
			if (_medicao.UsuarioId.HasValue) Log($"(esta pesagem é do usuário nº {_medicao.UsuarioId})");
			if (_ativo != null) _medicao.CalcularImc(_ativo.AlturaCm);
			MainThread.BeginInvokeOnMainThread(MostrarResultado);
			// Já temos PESO de agora + composição: aí sim encerramos.
			if (_pesoAgora) { _pesagemRecebida = true; Log("✔ Composição de agora recebida."); }
		}
	}

	void MostrarResultado()
	{
		if (!_medicao.TemAlgo) return;

		ResultadoCard.IsVisible = true;
		StatusLabel.Text = "Medição recebida! Confira abaixo.";

		PesoLabel.Text = _medicao.PesoKg.HasValue ? Num(_medicao.PesoKg.Value, 1) : "--";

		if (_medicao.Quando.HasValue)
		{
			var quando = _medicao.Quando.Value;
			bool recente = (DateTime.Now - quando).TotalMinutes < 2;
			QuandoLabel.Text = recente
				? $"Pesagem agora ({quando:HH:mm})"
				: $"Pesagem de {quando:dd/MM HH:mm} (a mais recente guardada)";
		}
		else
		{
			QuandoLabel.Text = string.Empty;
		}

		Grade.Children.Clear();
		Grade.RowDefinitions.Clear();

		var itens = new List<(string, string)>();
		if (_medicao.ImcCalculado.HasValue) itens.Add(("IMC", Num(_medicao.ImcCalculado.Value, 1)));
		if (_medicao.GorduraPct.HasValue) itens.Add(("Gordura", Num(_medicao.GorduraPct.Value, 1) + " %"));
		if (_medicao.MusculoPct.HasValue) itens.Add(("Músculo", Num(_medicao.MusculoPct.Value, 1) + " %"));
		if (_medicao.AguaMassaKg.HasValue) itens.Add(("Água", Num(_medicao.AguaMassaKg.Value, 1) + " kg"));
		if (_medicao.MassaMagraKg.HasValue) itens.Add(("Massa magra", Num(_medicao.MassaMagraKg.Value, 1) + " kg"));
		if (_medicao.BasalKcal.HasValue) itens.Add(("Metabolismo", Num(_medicao.BasalKcal.Value, 0) + " kcal"));
		if (_medicao.ImpedanciaOhm.HasValue) itens.Add(("Impedância", Num(_medicao.ImpedanciaOhm.Value, 0) + " Ω"));

		for (int k = 0; k < itens.Count; k++)
		{
			int row = k / 2, col = k % 2;
			if (col == 0) Grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			var tile = new VerticalStackLayout { Spacing = 2 };
			tile.Children.Add(new Label { Text = itens[k].Item1, FontSize = 12, TextColor = Color.FromArgb("#9E9E9E") });
			tile.Children.Add(new Label { Text = itens[k].Item2, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#ECECEC") });

			Grade.Add(tile, col, row);
		}
	}

	async void OnEnviarGarminClicked(object sender, EventArgs e)
	{
		if (_enviando) return;

		if (_ativo == null)
		{
			await DisplayAlert("Perfil", "Escolha um perfil antes de enviar.", "OK");
			return;
		}
		if (_medicao.PesoKg is not > 0)
		{
			await DisplayAlert("Sem dados", "Faça uma pesagem antes de enviar.", "OK");
			return;
		}

		var senha = await PerfilStore.LerSenhaAsync(_ativo.Id);
		if (string.IsNullOrWhiteSpace(_ativo.GarminEmail) || string.IsNullOrWhiteSpace(senha))
		{
			bool ir = await DisplayAlert("Login do Garmin",
				"Este perfil ainda não tem e-mail e senha do Garmin. Quer editar o perfil agora?", "Editar", "Cancelar");
			if (ir) await Navigation.PushAsync(new PerfilEditPage(_ativo.Id));
			return;
		}

		_enviando = true;
		EnviarBtn.IsEnabled = false;
		EnviarBtn.Text = "Enviando...";
		MostrarEnvio("Entrando na sua conta do Garmin e enviando...");

		try
		{
			// Roda o envio FORA da thread da tela para o app não travar durante o
			// login do Garmin (que é pesado).
			var med = _medicao; var perfil = _ativo; var email = _ativo.GarminEmail;
			var r = await Task.Run(() => _garmin.EnviarAsync(med, perfil, email, senha));

			// Se o Garmin pedir código de verificação (2FA), pergunta e tenta de novo.
			while (r.PrecisaCodigo)
			{
				var codigo = await DisplayPromptAsync("Verificação (2FA)",
					"O Garmin enviou um código (SMS/app/e-mail). Digite-o:",
					accept: "Enviar", cancel: "Cancelar", keyboard: Keyboard.Numeric);

				if (string.IsNullOrWhiteSpace(codigo))
				{
					MostrarEnvio("Envio cancelado (código não informado).");
					return;
				}
				MostrarEnvio("Conferindo o código e enviando...");
				var cod = codigo.Trim();
				r = await Task.Run(() => _garmin.EnviarAsync(med, perfil, email, senha, cod));
			}

			MostrarEnvio(r.Mensagem);
			if (r.Sucesso)
				await DisplayAlert("Pronto!", "Pesagem enviada pro Garmin Connect. ✅", "OK");
			else
				await DisplayAlert("Não enviou", r.Mensagem, "OK");
		}
		catch (Exception ex)
		{
			MostrarEnvio("Erro inesperado ao enviar.");
			Log("Erro no envio Garmin: " + ex.Message);
		}
		finally
		{
			_enviando = false;
			EnviarBtn.IsEnabled = true;
			EnviarBtn.Text = "Enviar pro Garmin";
		}
	}

	void MostrarEnvio(string texto)
	{
		EnvioLabel.Text = texto;
		EnvioLabel.IsVisible = !string.IsNullOrEmpty(texto);
	}

	static string Num(double v, int casas) => v.ToString("F" + casas, CultureInfo.InvariantCulture).Replace('.', ',');

	async Task<bool> EnsurePermissionsAsync()
	{
#if ANDROID
		var status = await Permissions.RequestAsync<BlePermissions>();
		return status == PermissionStatus.Granted;
#else
		return await Task.FromResult(true);
#endif
	}
}

#if ANDROID
public class BlePermissions : Permissions.BasePlatformPermission
{
	public override (string androidPermission, bool isRuntime)[] RequiredPermissions
	{
		get
		{
			var permissoes = new List<(string, bool)>();
			if (OperatingSystem.IsAndroidVersionAtLeast(31))
			{
				permissoes.Add((global::Android.Manifest.Permission.BluetoothScan, true));
				permissoes.Add((global::Android.Manifest.Permission.BluetoothConnect, true));
			}
			else
			{
				permissoes.Add((global::Android.Manifest.Permission.AccessFineLocation, true));
			}
			return permissoes.ToArray();
		}
	}
}
#endif
