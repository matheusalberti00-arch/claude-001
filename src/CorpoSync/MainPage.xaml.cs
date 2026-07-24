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

	readonly IBluetoothLE _ble;
	readonly IAdapter _adapter;
	readonly HashSet<string> _seen = new();
	readonly Dictionary<string, ICharacteristic> _canais = new();

	List<Perfil> _perfis = new();
	Perfil? _ativo;
	bool _scanning;
	bool _capturaFresca;
	TaskCompletionSource<byte[]>? _respostaUcp;
	Medicao _medicao = new();

	public MainPage()
	{
		InitializeComponent();
		_ble = CrossBluetoothLE.Current;
		_adapter = CrossBluetoothLE.Current.Adapter;
		_adapter.ScanTimeout = 30000;
		_adapter.DeviceDiscovered += OnDeviceDiscovered;
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
		_capturaFresca = false;
		ResultadoCard.IsVisible = false;
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
			await _adapter.ConnectToDeviceAsync(device);
			Log("Conectado. Preparando os canais...");

			var services = await device.GetServicesAsync();
			foreach (var s in services)
				foreach (var c in await s.GetCharacteristicsAsync())
					_canais[Curto(c.Id)] = c;

			await OuvirAsync(UCP, OnUcpAtualizado);
			await OuvirAsync(PESO, OnMedicaoRecebida);
			await OuvirAsync(COMP, OnMedicaoRecebida);

			byte cLo = (byte)(CODIGO_CONSENTIMENTO & 0xFF);
			byte cHi = (byte)((CODIGO_CONSENTIMENTO >> 8) & 0xFF);

			int idx = _ativo.ScaleUserIndex;

			if (idx >= 0)
			{
				Log($"Usuário na balança nº {idx}. Consentindo...");
				var r = await EscreverUcpEsperar(new byte[] { 0x02, (byte)idx, cLo, cHi }, "Consentir");
				if (!(r != null && r.Length >= 3 && r[0] == 0x20 && r[2] == 0x01))
				{
					Log("Consentimento falhou; registrando novo usuário.");
					idx = -1;
				}
			}

			if (idx < 0)
			{
				var r = await EscreverUcpEsperar(new byte[] { 0x01, cLo, cHi }, "Registrar novo usuário");
				if (r != null && r.Length >= 4 && r[0] == 0x20 && r[2] == 0x01)
				{
					idx = r[3];
					SalvarIndiceNoPerfil(idx);
					Log($"✔ Usuário criado na balança (nº {idx}).");
				}
				else
				{
					Log("✖ Não consegui registrar o usuário na balança.");
				}
			}

			if (idx >= 0)
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

				MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = "Pronto! Suba na balança e fique parado.");
				Log("Handshake completo! Suba na balança.");
			}
		}
		catch (Exception ex)
		{
			Log("Erro ao conectar/identificar: " + ex.Message);
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
			// A balança entrega a última pesagem GUARDADA assim que conecta.
			// Só mostramos se for uma pesagem NOVA (carimbo de hora recente).
			var tmp = new Medicao();
			tmp.LerPeso(bytes);
			bool fresca = !tmp.Quando.HasValue
				|| (DateTime.Now - tmp.Quando.Value).TotalSeconds <= 90;

			if (fresca)
			{
				if (!_capturaFresca) _medicao = new Medicao();
				_capturaFresca = true;
				_medicao.LerPeso(bytes);
				if (_ativo != null) _medicao.CalcularImc(_ativo.AlturaCm);
				MainThread.BeginInvokeOnMainThread(MostrarResultado);
			}
			else
			{
				_capturaFresca = false;
				var hora = tmp.Quando?.ToString("dd/MM HH:mm") ?? "?";
				MainThread.BeginInvokeOnMainThread(() =>
				{
					StatusLabel.Text = $"Recebi a pesagem guardada de {hora}. Suba na balança AGORA para uma nova.";
				});
			}
		}
		else if (canal == COMP && _capturaFresca)
		{
			_medicao.LerComposicao(bytes);
			if (_ativo != null) _medicao.CalcularImc(_ativo.AlturaCm);
			MainThread.BeginInvokeOnMainThread(MostrarResultado);
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
				: $"⚠ Pesagem guardada de {quando:dd/MM HH:mm}. Suba na balança para uma nova.";
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
