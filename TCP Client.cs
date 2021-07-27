using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TCP_Client
{
	/// <summary>
	/// TCP非同期送受信Class
	/// </summary>
	internal class TCPClient : IDisposable, IAsyncDisposable
	{
		/// <summary>
		/// 各イベントのタイムアウト
		/// </summary>
		private const int timeOut = 10000;

		/// <summary>
		/// エラー発生時のメッセージ
		/// </summary>
		private readonly bool isMessage;

		/// <summary>
		/// TCP接続で非同期処理された時にセットされるイベント
		/// </summary>
		private readonly ManualResetEventSlim connectDone = new(false);

		/// <summary>
		/// TCP送信で非同期処理された時にセットされるイベント
		/// </summary>
		private readonly ManualResetEventSlim sendDone = new(false);

		/// <summary>
		/// TCP受信で非同期処理された時にセットされるイベント
		/// </summary>
		private readonly ManualResetEventSlim receiveDone = new(false);

		/// <summary>
		/// ログ出力用デリゲートのメソッド型
		/// </summary>
		/// <param name="message"></param>
		internal delegate void LogDelegate(string message);

		/// <summary>
		/// ログ出力デリゲート
		/// </summary>
		internal LogDelegate logDelegate = null;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="Log">ログ出力用デリゲート</param>
		/// <param name="IsMessage">エラーメッセージ false=表示しない true=表示する</param>
		internal TCPClient(LogDelegate Log = null, bool IsMessage = false)
		{
			isMessage = IsMessage;
			// ログを記録する設定ならLog Classのインスタンスを生成
			if (Log != null)
			{
				logDelegate = Log;
			}
		}

		/// <summary>
		/// デストラクタ
		/// </summary>
		~TCPClient()
		{
			// usingやDispose忘れがあるかもしれないので念のため
			connectDone?.Dispose();
			sendDone?.Dispose();
			receiveDone?.Dispose();
		}

		/// <summary>
		/// 終了時の処理
		/// </summary>
		public void Dispose()
		{
			// 事後処理
			connectDone?.Dispose();
			sendDone?.Dispose();
			receiveDone?.Dispose();
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 非同期な終了処理
		/// </summary>
		/// <returns></returns>
		public ValueTask DisposeAsync()
		{
			// 事後処理
			connectDone?.Dispose();
			sendDone?.Dispose();
			receiveDone?.Dispose();
			GC.SuppressFinalize(this);
			return default;
		}

		/// <summary>
		/// データ送受信
		/// </summary>
		/// <param name="address">接続するサーバのIPアドレス</param>
		/// <param name="port">接続するサーバのポート番号</param>
		/// <param name="sendData">送信するデータの配列</param>
		/// <param name="receiveData">受信したデータを入れる配列の参照</param>
		/// <returns></returns>
		internal bool SendReceive(string address, int port, byte[] sendData, ref byte[] receiveData)
		{
			// IPアドレスのチェック
			if (IPAddress.TryParse(address, out IPAddress serverAddress) == false)
			{
				logDelegate?.Invoke("IPアドレスが正しくありません。");
				if (isMessage == true)
					_ = MessageBox.Show("IPアドレスが正しくありません。", "データ送受信", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			// 送信データのチェック
			if (sendData == null || sendData.Length == 0)
			{
				logDelegate?.Invoke("データ送信,送信するデータがありません。");
				if (isMessage == true)
					_ = MessageBox.Show("送信するデータがありません。", "データ送信", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			bool result = true;
			try
			{
				using Socket client = new Socket(serverAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				try
				{
					using SocketAsyncEventArgs e = new();
					// ConnectAsync前にSetBufferメソッドを実行するとConnectAsync実行後自動的にデータが送信される
					e.SetBuffer(sendData, 0, sendData.Length);
					e.RemoteEndPoint = new IPEndPoint(serverAddress, port);
					e.Completed += new EventHandler<SocketAsyncEventArgs>(Completed);
					/***** TCPサーバへ接続 *****/
					if (client.ConnectAsync(e) == true)
					{
						// 接続がタイムアウトなら
						if (connectDone.Wait(timeOut) == false)
						{
							throw new InvalidOperationException("PLC 接続タイムアウト");
						}
					}
					/***** データ送信 *****/
					// 何かしら理由があり送信を任意に行いたい場合はConnectAsync前にSetBufferメソッドを実行せず、送信直前にSetBufferメソッドを実行する
					//e.SetBuffer(sendData, 0, sendData.Length);
					//if (client.SendAsync(e) == true)
					//{
					//	if (sendDone.Wait(timeOut) == false)
					//	{
					//		throw new InvalidOperationException("PLC データ送信タイムアウト");
					//	}
					//}
					/***** データ受信 *****/
					e.SetBuffer(receiveData, 0, receiveData.Length);
					if (client.ReceiveAsync(e) == true)
					{
						if (receiveDone.Wait(timeOut) == false)
						{
							throw new InvalidOperationException("PLC データ受信タイムアウト");
						}
					}
				}
				catch (Exception e)
				{
					result = false;
					logDelegate?.Invoke("データ送受信例外," + e.Message);
					if (isMessage == true)
						_ = MessageBox.Show(e.Message, "例外エラー", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					//Socketでの送受信を無効にする
					client.Shutdown(SocketShutdown.Both);
					// ソケット接続を閉じ、ソケットを再利用できるようにする。再利用できる場合は true それ以外の場合は false
					client.Disconnect(true);
				}
			}
			catch (Exception e)
			{
				logDelegate?.Invoke("データ送受信例外," + e.Message);
				if (isMessage == true)
					_ = MessageBox.Show(e.Message, "例外エラー", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			return result;
		}

		/// <summary>
		/// SocketAsyncEventArgsのCompletedイベント
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Completed(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Connect:
					connectDone.Set();
					break;
				case SocketAsyncOperation.Send:
					sendDone.Set();
					break;
				case SocketAsyncOperation.Receive:
					receiveDone.Set();
					break;
			}
		}
	}
}
