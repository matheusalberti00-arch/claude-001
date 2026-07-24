using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace CorpoSync;

public partial class MainPage : ContentPage
{
	readonly IBluetoothLE _ble;
	readonly IAdapter _adapter;
	readonly HashSet<string> _seen = new();
	bool _scanning;

	public MainPage()
	{
		InitializeComponent();

		_ble = CrossBluetoothLE.Current;
		_adapter = CrossBluetoothLE.Current.Adapter;
		_adapter.ScanTimeout = 30000; // 30 segundos procurando
		_adapter.DeviceDiscovered += OnDeviceDiscovered;
	}

	// Escreve uma linha no "registro" da tela (com horário).
	void Log(string msg)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			LogLabel.Text += $"{DateTime.Now:HH:mm:ss}  {msg}\n";
			await LogScroll.ScrollToAsync(LogLabel, ScrollToPosition.End, false);
		});
	}

	async void OnScanClicked(object sender, EventArgs e)
	{
		if (_scanning)
			return;

		LogLabel.Text = string.Empty;
		_seen.Clear();

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
		Log("Iniciando busca por aparelhos Bluetooth...");

		try
		{
			await _adapter.StartScanningForDevicesAsync();
		}
		catch (Exception ex)
		{
			Log("Erro na busca: " + ex.Message);
		}

		_scanning = false;
		StatusLabel.Text = "Busca encerrada.";
		Log("Busca encerrada.");
	}

	async void OnStopClicked(object sender, EventArgs e)
	{
		try
		{
			await _adapter.StopScanningForDevicesAsync();
		}
		catch
		{
			// ignora
		}
	}

	void OnDeviceDiscovered(object? sender, DeviceEventArgs a)
	{
		var d = a.Device;
		var id = d.Id.ToString();
		if (!_seen.Add(id))
			return; // já listado

		var name = string.IsNullOrWhiteSpace(d.Name) ? "(sem nome)" : d.Name;
		Log($"Achei: {name}   sinal {d.Rssi}   [{id}]");

		// Tenta reconhecer a balança pelo nome e conectar automaticamente.
		var lname = (d.Name ?? string.Empty).ToLowerInvariant();
		if (lname.Contains("bf") || lname.Contains("beurer")
			|| lname.Contains("scale") || lname.Contains("balan"))
		{
			Log($"→ Parece a balança! Conectando em \"{name}\"...");
			_ = ConnectAndListenAsync(d);
		}
	}

	async Task ConnectAndListenAsync(IDevice device)
	{
		try
		{
			await _adapter.StopScanningForDevicesAsync();
			await _adapter.ConnectToDeviceAsync(device);
			Log("Conectado. Lendo serviços da balança...");

			var services = await device.GetServicesAsync();
			foreach (var s in services)
			{
				Log($"— Serviço {Curto(s.Id)}");
				var chars = await s.GetCharacteristicsAsync();
				foreach (var c in chars)
				{
					// Mostra o canal e o que ele sabe fazer (avisar/ler/escrever).
					Log($"   canal {Curto(c.Id)}  [{c.Properties}]");

					// Se o canal manda avisos (notify/indicate), passamos a ouvir.
					if (c.CanUpdate)
					{
						c.ValueUpdated += (o, args) =>
						{
							var bytes = args.Characteristic.Value;
							var hex = (bytes == null || bytes.Length == 0)
								? "(vazio)"
								: BitConverter.ToString(bytes);
							Log($"DADO {Curto(c.Id)}: {hex}");
						};

						try { await c.StartUpdatesAsync(); }
						catch (Exception ex) { Log($"   (não deu p/ ouvir {Curto(c.Id)}: {ex.Message})"); }
					}

					// Se o canal pode ser lido, lemos uma vez o valor atual.
					if (c.CanRead)
					{
						try
						{
							var (data, _) = await c.ReadAsync();
							var hex = (data == null || data.Length == 0)
								? "(vazio)"
								: BitConverter.ToString(data);
							Log($"   leitura {Curto(c.Id)}: {hex}");
						}
						catch
						{
							// alguns canais não deixam ler; tudo bem
						}
					}
				}
			}

			Log("Pronto! Fique EM CIMA da balança até ela mostrar o resultado final.");
			StatusLabel.Text = "Conectado. Suba e permaneça na balança.";
		}
		catch (Exception ex)
		{
			Log("Erro ao conectar: " + ex.Message);
		}
	}

	// Mostra a parte curta de um UUID padrão (ex.: 2a9d) para o registro ficar legível.
	static string Curto(Guid g)
	{
		var s = g.ToString();
		return s.Length >= 8 ? s.Substring(4, 4) : s;
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
// Permissões de Bluetooth. No Android 12+ (API 31) usa-se "Dispositivos por perto"
// (BLUETOOTH_SCAN/CONNECT) e NÃO se pede localização. No Android 11 e anteriores,
// o scan BLE exige localização.
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
