using Microsoft.Maui.Storage;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace CorpoSync;

public partial class MainPage : ContentPage
{
	// ---- Dados do usuário (fixos por enquanto; tela de edição vem numa etapa futura) ----
	const byte SEXO = 0x00;            // 0 = masculino, 1 = feminino
	const int ANO_NASC = 1996;
	const byte MES_NASC = 1;           // janeiro
	const byte DIA_NASC = 3;
	const int ALTURA_CM = 170;
	const int CODIGO_CONSENTIMENTO = 0x1717; // "senha" que o app usa com a balança

	// ---- UUIDs curtos dos canais que interessam ----
	const string UCP = "2a9f";   // User Control Point (identificação do usuário)
	const string PESO = "2a9d";  // Weight Measurement
	const string COMP = "2a9c";  // Body Composition Measurement
	const string SEXO_CH = "2a8c";
	const string NASC_CH = "2a85";
	const string ALT_CH = "2a8e";

	readonly IBluetoothLE _ble;
	readonly IAdapter _adapter;
	readonly HashSet<string> _seen = new();
	readonly Dictionary<string, ICharacteristic> _canais = new();
	bool _scanning;
	TaskCompletionSource<byte[]>? _respostaUcp;

	public MainPage()
	{
		InitializeComponent();

		_ble = CrossBluetoothLE.Current;
		_adapter = CrossBluetoothLE.Current.Adapter;
		_adapter.ScanTimeout = 30000;
		_adapter.DeviceDiscovered += OnDeviceDiscovered;
	}

	void Log(string msg)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			LogLabel.Text += $"{DateTime.Now:HH:mm:ss}  {msg}\n";
			await LogScroll.ScrollToAsync(LogLabel, ScrollToPosition.End, false);
		});
	}

	static string Hex(byte[]? b) =>
		(b == null || b.Length == 0) ? "(vazio)" : BitConverter.ToString(b);

	static string Curto(Guid g)
	{
		var s = g.ToString();
		return s.Length >= 8 ? s.Substring(4, 4) : s;
	}

	async void OnScanClicked(object sender, EventArgs e)
	{
		if (_scanning)
			return;

		LogLabel.Text = string.Empty;
		_seen.Clear();
		_canais.Clear();

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
		StatusLabel.Text = "Procurando... suba na balança agora.";
		Log("Procurando a balança...");

		try
		{
			await _adapter.StartScanningForDevicesAsync();
		}
		catch (Exception ex)
		{
			Log("Erro na busca: " + ex.Message);
		}

		_scanning = false;
	}

	async void OnStopClicked(object sender, EventArgs e)
	{
		try { await _adapter.StopScanningForDevicesAsync(); }
		catch { }
	}

	void OnDeviceDiscovered(object? sender, DeviceEventArgs a)
	{
		var d = a.Device;
		var id = d.Id.ToString();
		if (!_seen.Add(id))
			return;

		var lname = (d.Name ?? string.Empty).ToLowerInvariant();
		if (lname.Contains("bf") || lname.Contains("beurer")
			|| lname.Contains("scale") || lname.Contains("balan"))
		{
			Log($"Achei a balança: {d.Name}. Conectando...");
			_ = ConectarEIdentificarAsync(d);
		}
	}

	async Task ConectarEIdentificarAsync(IDevice device)
	{
		try
		{
			await _adapter.StopScanningForDevicesAsync();
			await _adapter.ConnectToDeviceAsync(device);
			Log("Conectado. Preparando os canais...");

			// Mapeia todos os canais por UUID curto.
			var services = await device.GetServicesAsync();
			foreach (var s in services)
			{
				foreach (var c in await s.GetCharacteristicsAsync())
					_canais[Curto(c.Id)] = c;
			}

			// Passa a ouvir: identificação (UCP), peso e composição.
			await OuvirAsync(UCP, OnUcpAtualizado);
			await OuvirAsync(PESO, OnMedicao);
			await OuvirAsync(COMP, OnMedicao);

			// ---- Handshake: identificar-se como usuário da balança ----
			byte cLo = (byte)(CODIGO_CONSENTIMENTO & 0xFF);
			byte cHi = (byte)((CODIGO_CONSENTIMENTO >> 8) & 0xFF);

			int idxUsuario = Preferences.Get("userIndex", -1);

			if (idxUsuario >= 0)
			{
				Log($"Usuário salvo (nº {idxUsuario}). Fazendo consentimento...");
				var r = await EscreverUcpEsperar(new byte[] { 0x02, (byte)idxUsuario, cLo, cHi }, "Consentir usuário");
				if (!(r != null && r.Length >= 3 && r[0] == 0x20 && r[2] == 0x01))
				{
					Log("Consentimento falhou. Vou registrar um usuário novo.");
					idxUsuario = -1;
					Preferences.Remove("userIndex");
				}
			}

			if (idxUsuario < 0)
			{
				var r = await EscreverUcpEsperar(new byte[] { 0x01, cLo, cHi }, "Registrar novo usuário");
				if (r != null && r.Length >= 4 && r[0] == 0x20 && r[2] == 0x01)
				{
					idxUsuario = r[3];
					Preferences.Set("userIndex", idxUsuario);
					Log($"✔ Usuário criado na balança (nº {idxUsuario}).");
				}
				else
				{
					Log("✖ Não consegui registrar o usuário. Anote o que apareceu e me mande.");
				}
			}

			// ---- Preenche o perfil (sexo, nascimento, altura) ----
			if (idxUsuario >= 0)
			{
				await EscreverPerfil(SEXO_CH, new byte[] { SEXO }, "sexo");
				await EscreverPerfil(NASC_CH, new byte[]
				{
					(byte)(ANO_NASC & 0xFF), (byte)((ANO_NASC >> 8) & 0xFF), MES_NASC, DIA_NASC
				}, "nascimento");
				await EscreverPerfil(ALT_CH, new byte[]
				{
					(byte)(ALTURA_CM & 0xFF), (byte)((ALTURA_CM >> 8) & 0xFF)
				}, "altura");

				Log("Handshake completo! Agora SUBA na balança e fique parado.");
				StatusLabel.Text = "Identificado. Suba na balança.";
			}
		}
		catch (Exception ex)
		{
			Log("Erro ao conectar/identificar: " + ex.Message);
		}
	}

	async Task OuvirAsync(string uuidCurto, EventHandler<CharacteristicUpdatedEventArgs> handler)
	{
		if (!_canais.TryGetValue(uuidCurto, out var c))
		{
			Log($"(canal {uuidCurto} não encontrado)");
			return;
		}
		try
		{
			c.ValueUpdated += handler;
			await c.StartUpdatesAsync();
		}
		catch (Exception ex)
		{
			Log($"(não deu p/ ouvir {uuidCurto}: {ex.Message})");
		}
	}

	async Task<byte[]?> EscreverUcpEsperar(byte[] cmd, string desc)
	{
		if (!_canais.TryGetValue(UCP, out var ucp))
		{
			Log("Canal de identificação (2a9f) não achado.");
			return null;
		}

		_respostaUcp = new TaskCompletionSource<byte[]>();
		Log($"→ {desc}: {Hex(cmd)}");
		try
		{
			ucp.WriteType = CharacteristicWriteType.WithResponse;
			await ucp.WriteAsync(cmd);
		}
		catch (Exception ex)
		{
			Log($"   erro ao escrever ({desc}): {ex.Message}");
			return null;
		}

		var done = await Task.WhenAny(_respostaUcp.Task, Task.Delay(8000));
		if (done == _respostaUcp.Task)
			return _respostaUcp.Task.Result;

		Log($"   (sem resposta em 8s para: {desc})");
		return null;
	}

	async Task EscreverPerfil(string uuidCurto, byte[] valor, string nome)
	{
		if (!_canais.TryGetValue(uuidCurto, out var c))
		{
			Log($"(canal de {nome} não achado)");
			return;
		}
		try
		{
			c.WriteType = CharacteristicWriteType.WithResponse;
			await c.WriteAsync(valor);
			Log($"Perfil: {nome} = {Hex(valor)}");
		}
		catch (Exception ex)
		{
			Log($"(não deu p/ gravar {nome}: {ex.Message})");
		}
	}

	void OnUcpAtualizado(object? sender, CharacteristicUpdatedEventArgs args)
	{
		var bytes = args.Characteristic.Value ?? Array.Empty<byte>();
		Log($"Resposta da balança (2a9f): {Hex(bytes)}");
		_respostaUcp?.TrySetResult(bytes);
	}

	void OnMedicao(object? sender, CharacteristicUpdatedEventArgs args)
	{
		var canal = Curto(args.Characteristic.Id);
		var bytes = args.Characteristic.Value;
		Log($"DADO {canal}: {Hex(bytes)}");
	}

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
