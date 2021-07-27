using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Text;

namespace TCP_Client.ViewModels
{
	public class MainWindowViewModel : BindableBase, INotifyPropertyChanged
	{
		/// <summary>
		/// ウィンドウに表示するタイトル
		/// </summary>
		public ReactivePropertySlim<string> Title { get; } = new ReactivePropertySlim<string>("TCP Client");

		/// <summary>
		/// Disposeが必要な処理をまとめてやる
		/// </summary>
		private CompositeDisposable Disposable { get; } = new CompositeDisposable();

		/// <summary>
		/// MainWindowのCloseイベント
		/// </summary>
		public ReactiveCommand ClosedCommand { get; } = new ReactiveCommand();

		/// <summary>
		/// TCP Client データ送受信コマンド
		/// </summary>
		public ReactiveCommand SendCommand { get; } = new ReactiveCommand();

		/// <summary>
		/// 送信するデータ
		/// </summary>
		public ReactivePropertySlim<string> SendData { get; } = new ReactivePropertySlim<string>(string.Empty);

		/// <summary>
		/// 受信したデータ
		/// </summary>
		public ReactivePropertySlim<string> ReceptionData { get; } = new ReactivePropertySlim<string>(string.Empty);

		/// <summary>
		/// コンストラクタ
		/// </summary>
		public MainWindowViewModel()
		{
			_ = SendCommand.Subscribe(TcpTransmission).AddTo(Disposable);
			_ = ClosedCommand.Subscribe(Close).AddTo(Disposable);
		}

		/// <summary>
		/// 送受信メソッド
		/// </summary>
		private async void TcpTransmission()
		{
			await using TCPClient Client = new TCPClient();
			byte[] sendData = Encoding.ASCII.GetBytes(SendData.Value);
			// 想定される受信データサーズから受信データを入れる入れ物のサイズを決めておく
			byte[] receiveData = new byte[1024];
			// 戻り値がTrueなら正常に送受信完了
			await Task.Run(() =>
			{
				if (Client.SendReceive("127.0.0.1", 50000, sendData, ref receiveData) == true)
				{
					// 受信した文字列データは用意している入れ物より少ない場合もあるので「.TrimEnd('\0')」する。
					ReceptionData.Value = Encoding.ASCII.GetString(receiveData).TrimEnd('\0');
				}
			});
		}

		/// <summary>
		/// アプリが閉じられる時
		/// </summary>
		private void Close()
		{
			Disposable.Dispose();
		}
	}
}