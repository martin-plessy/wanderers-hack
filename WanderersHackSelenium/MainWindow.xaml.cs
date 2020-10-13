using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PuppeteerSharp;
using System.Globalization;
using System.Timers;
using System.ComponentModel;

namespace WanderersHackSelenium
{
	public class MinionHealthColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return 2 < (int) value
				? new SolidColorBrush(Color.FromRgb(153, 229, 80))
				: new SolidColorBrush(Color.FromRgb(217, 87, 99));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public partial class MainWindow : Window, IAsyncDisposable, INotifyPropertyChanged
	{
		public MainWindow()
		{
			InitializeComponent();

			DataContext = this;

			InitializePuppeteer();
		}

		Wanderer WandererInstance = null;
		bool IsDown = false;
		public List<Spy> Spies { get; set; }
		Timer PeriodicSpy;

		public event PropertyChangedEventHandler PropertyChanged;

		void InitializePuppeteer()
		{
			Closing += async (_, __) =>
			{
				try
				{
					await DisposeAsync();
				}
				catch (Exception e)
				{
					MessageBox.Show($"{ e.GetType().Name }\n----\n{ e.Message }\n----\n{ e.StackTrace }");
					throw;
				}
			};

			AppDomain.CurrentDomain.UnhandledException += async (_, @event) =>
			{
				var error = @event.ExceptionObject as Exception;

				MessageBox.Show($"{ error.GetType().Name }\n----\n{ error.Message }\n----\n{ error.StackTrace }");

				try
				{
					await DisposeAsync();
				}
				catch (Exception e)
				{
					MessageBox.Show($"{ e.GetType().Name }\n----\n{ e.Message }\n----\n{ e.StackTrace }");
					throw;
				}

				throw error;
			};

			SetSelection = new SetSelectionCommand { Upper = this };
			WandererInstance = new Wanderer();

			Task.Run(async () =>
			{
				await Task.WhenAll(
					WandererInstance.NewAsync(),
					WandererInstance.NewAsync()
				);

				Spies = (await WandererInstance.SpyAsync()).ToList();
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Spies"));

				PeriodicSpy = new Timer(2000);

				PeriodicSpy.Elapsed += async (_, __) =>
				{
					Spies = (await WandererInstance.SpyAsync()).ToList();
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Spies"));
				};

				TheCanvas.Dispatcher.Invoke(() =>
				{
					SpyList.SelectedIndex = 0;

					TheCanvas.MouseMove += async (_, @event) =>
					{
						if (IsDown)
						{
							Point mouse = @event.GetPosition(TheCanvas);

							await WandererInstance.MoveAsync((int) mouse.X, (int) mouse.Y);
						}
					};

					TheCanvas.MouseDown += async (_, @event) =>
					{
						Point mouse = @event.GetPosition(TheCanvas);

						await WandererInstance.DownAsync((int) mouse.X, (int) mouse.Y);

						IsDown = true;
					};

					TheCanvas.MouseUp += async (_, @event) =>
					{
						Point mouse = @event.GetPosition(TheCanvas);

						await WandererInstance.UpAsync((int) mouse.X, (int) mouse.Y);

						IsDown = false;
					};

					ChatBox.KeyUp += async (_, @event) =>
					{
						if (@event.Key == System.Windows.Input.Key.Enter)
						{
							string message = ChatBox.Text.Trim();

							if (!string.IsNullOrEmpty(message))
								await WandererInstance.MessageAsync(message);

							ChatBox.Text = string.Empty;
							@event.Handled = true;
						}
						else
						{
							@event.Handled = false;
						}
					};

					AddButton.Click += async (_, @event) =>
					{
						await WandererInstance.NewAsync();
					};
				});

				PeriodicSpy.Start();
			}).ContinueWith((task, __) =>
			{
				var error = task.Exception;

				if (error != null)
				{
					MessageBox.Show($"{ error.GetType().Name }\n----\n{ error.Message }\n----\n{ error.StackTrace }");

					try
					{
						DisposeAsync().GetAwaiter().GetResult();
					}
					catch (Exception e)
					{
						MessageBox.Show($"{ e.GetType().Name }\n----\n{ e.Message }\n----\n{ e.StackTrace }");
						throw;
					}

					throw error;
				}
			}, TaskContinuationOptions.OnlyOnFaulted);
		}

		class SetSelectionCommand : System.Windows.Input.ICommand
		{
			public MainWindow Upper;
			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter) => Upper.WandererInstance != null;

			public async void Execute(object parameter)
			{
				await Upper.WandererInstance.SelectAsync((int) parameter);

				Upper.Spies = (await Upper.WandererInstance.SpyAsync()).ToList();
				Upper.PropertyChanged?.Invoke(Upper, new PropertyChangedEventArgs("Spies"));
			}
		}

		public System.Windows.Input.ICommand SetSelection { get; set; }

		public async ValueTask DisposeAsync()
		{
			PeriodicSpy?.Stop();
			PeriodicSpy?.Dispose();

			if (WandererInstance != null)
				await WandererInstance.DisposeAsync();
		}

		public class Spy
		{
			public int Index { get; set; }
			public string Status { get; set; }
			public string Name{ get; set; }
			public int Food { get; set; }
			public int Wood { get; set; }
			public int Gold { get; set; }
			public int Water { get; set; }
			public int Level { get; set; }
			public int Dangle { get; set; }
			public float TotemX { get; set; }
			public float TotemY { get; set; }
			public List<Minion> Minions { get; set; }
		}

		public class Minion
		{
			public int MaxHealth { get; set; }
			public int Health { get; set; }
		}

		public sealed class Wanderer : IAsyncDisposable
		{
			static string[] Names = new[] { "Origin", "Alfred", "Brook", "Ciara", "Daniel" };

			List<Page> Pages = new List<Page>(Names.Length);
			int SelectedIndex = 0;
			bool Exclusive = true;
			int I = 0;

			public async Task NewAsync()
			{
				if (I == 5) return;

				int i = I++;

				var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					ExecutablePath = @"C:\Program Files (x86)\Google\Chrome Dev\Application\chrome.exe",
					Devtools = false,
					Headless = false,
					IgnoreDefaultArgs = true,
					Args = new[]
					{
						"--disable-backgrounding-occluded-windows",
						"--disable-breakpad",
						"--disable-default-apps",
						"--disable-dev-shm-usage",
						"--disable-logging",
						"--disable-sync",
						"--incognito",
						"--no-default-browser-check",
						"--no-first-run",
						"--start-fullscreen"
					},
					DefaultViewport = new ViewPortOptions
					{
						Width = 1536,
						Height = 864
					}
				});

				Page page = (await browser.PagesAsync()).Single();

				await page.GoToAsync(@"https://wanderers.io");

				await page.WaitForSelectorAsync(".showMainMenu", new WaitForSelectorOptions { Visible = true });
				await page.ClickAsync(".showMainMenu");

				await page.WaitForSelectorAsync(".modePicker .ui-tabs a:nth-child(2)", new WaitForSelectorOptions { Visible = true });
				await page.WaitForSelectorAsync(".groupName", new WaitForSelectorOptions { Visible = true });
				await page.WaitForSelectorAsync(".tribeName", new WaitForSelectorOptions { Visible = true });
				await page.WaitForSelectorAsync(".start", new WaitForSelectorOptions { Visible = true });

				await page.ClickAsync(".modePicker .ui-tabs a:nth-child(2)");
				await page.TypeAsync(".groupName", "TeamCat");
				await page.TypeAsync(".tribeName", Names[i]);
				await page.ClickAsync(".start");

				await page.EvaluateExpressionAsync(@"
					function __$pyt4p__() {
						return (app.game.player && app.game.playerData && app.game.privateData && app.game.experienceBottle)
							? {
								status: "" "",
								name: app.game.player.shared.name,
								food: app.game.playerData.resources.food,
								wood: app.game.playerData.resources.wood,
								gold: app.game.playerData.resources.gold,
								water: app.game.playerData.resources.water,
								level: app.game.privateData.level,
								dangle: app.game.experienceBottle.levels,
								totemX: app.game.totem.x,
								totemY: app.game.totem.y,
								minions: app.game.player.members.filter(it => !it.dead).map(it => {
									return {
										maxHealth: it.maxHealth,
										health: it.shared.health
									}
								})
							}
							: {
								status: "" "",
								name: ""?"",
								food: 0,
								wood: 0,
								gold: 0,
								water: 0,
								level: 0,
								dangle: 0,
								totemX: 0,
								totemY: 0,
								minions: []
							}
					}
				");

				await page.WaitForSelectorAsync(".title", new WaitForSelectorOptions { Visible = true });

				Pages.Add(page);

				await SelectAsync(Pages.IndexOf(page));
			}

			public ValueTask DisposeAsync() => new ValueTask(Task.WhenAll(
				Pages.Select(async page =>
				{
					await page.Browser.CloseAsync();
					page.Browser.Dispose();
				})
			));

			public Task DownAsync(int x, int y)
			{
				return Exclusive
					? ExecuteAsync(Pages[SelectedIndex])
					: Task.WhenAll(Pages.Select(page => ExecuteAsync(page)));

				async Task ExecuteAsync(Page page)
				{
					await page.Mouse.MoveAsync(x, y);
					await page.Mouse.DownAsync();
				}
			}

			public Task MoveAsync(int x, int y)
			{
				return Exclusive
					? ExecuteAsync(Pages[SelectedIndex])
					: Task.WhenAll(Pages.Select(page => ExecuteAsync(page)));

				Task ExecuteAsync(Page page)
				{
					return page.Mouse.MoveAsync(x, y);
				}
			}

			public Task UpAsync(int x, int y)
			{
				return Exclusive
					? ExecuteAsync(Pages[SelectedIndex])
					: Task.WhenAll(Pages.Select(page => ExecuteAsync(page)));

				async Task ExecuteAsync(Page page)
				{
					await page.Mouse.MoveAsync(x, y);
					await page.Mouse.UpAsync();
				}
			}

			public Task SelectAsync(int i)
			{
				if (i == SelectedIndex)
				{
					Exclusive = !Exclusive;

					return Task.CompletedTask;
				}

				SelectedIndex = i;

				return Pages[i].BringToFrontAsync();
			}

			public Task MessageAsync(string message)
			{
				return Pages[SelectedIndex].EvaluateExpressionAsync($@"
					app.game.send(""quick_chat"", {{ text: ""{ message.Replace("\"", string.Empty) }"" }})
				");
			}

			public Task<Spy[]> SpyAsync() => Task.WhenAll(
				Pages.Select(async (page, i) =>
				{
					var spy = await page.EvaluateExpressionAsync<Spy>("__$pyt4p__()");

					spy.Index = i;
					spy.Status = SelectedIndex == i
						? Exclusive
							? "X"
							: "S"
						: spy.Dangle == 0
							? " "
							: spy.Dangle.ToString();

					return spy;
				})
			);
		}
	}
}
