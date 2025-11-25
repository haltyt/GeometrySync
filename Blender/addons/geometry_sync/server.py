"""
TCP Server for streaming mesh data to Unity
"""

import socket
import threading
import struct
import time
from typing import Optional, Callable


class MeshStreamServer:
    """TCP server that streams mesh data to Unity clients"""

    def __init__(self, host: str = '127.0.0.1', port: int = 8080):
        self.host = host
        self.port = port
        self.server_socket: Optional[socket.socket] = None
        self.client_socket: Optional[socket.socket] = None
        self.running = False
        self.server_thread: Optional[threading.Thread] = None
        self.lock = threading.Lock()

    def start(self):
        """Start the TCP server in a background thread"""
        if self.running:
            print("Server already running")
            return

        self.running = True
        self.server_thread = threading.Thread(target=self._run_server, daemon=True)
        self.server_thread.start()
        print(f"GeometrySync server started on {self.host}:{self.port}")

    def stop(self):
        """Stop the TCP server"""
        self.running = False

        if self.client_socket:
            try:
                self.client_socket.close()
            except:
                pass

        if self.server_socket:
            try:
                self.server_socket.close()
            except:
                pass

        if self.server_thread:
            self.server_thread.join(timeout=2.0)

        print("GeometrySync server stopped")

    def _run_server(self):
        """Server loop running in background thread"""
        try:
            self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.server_socket.bind((self.host, self.port))
            self.server_socket.listen(1)
            self.server_socket.settimeout(1.0)  # Allow checking self.running periodically

            print(f"Waiting for Unity client connection...")

            while self.running:
                try:
                    client, address = self.server_socket.accept()
                    print(f"Unity client connected from {address}")

                    # Enable TCP_NODELAY for reduced latency
                    client.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

                    with self.lock:
                        self.client_socket = client

                    # Keep connection alive until client disconnects
                    self._handle_client(client)

                except socket.timeout:
                    continue
                except Exception as e:
                    if self.running:
                        print(f"Server error: {e}")
                    break

        except Exception as e:
            print(f"Failed to start server: {e}")
        finally:
            if self.server_socket:
                self.server_socket.close()

    def _handle_client(self, client: socket.socket):
        """Handle connected client"""
        try:
            while self.running:
                # Just keep connection alive, actual sending happens via send_mesh()
                time.sleep(0.1)
        except:
            pass
        finally:
            with self.lock:
                self.client_socket = None
            print("Unity client disconnected")

    def send_mesh(self, mesh_data: bytes) -> bool:
        """
        Send mesh data to connected Unity client

        Args:
            mesh_data: Binary mesh data to send

        Returns:
            True if sent successfully, False otherwise
        """
        with self.lock:
            if not self.client_socket:
                return False

            try:
                # Message format: [type:1byte][length:4bytes][payload]
                message_type = 0x01  # Full mesh update
                length = len(mesh_data)
                header = struct.pack('<B I', message_type, length)  # < = little-endian

                self.client_socket.sendall(header + mesh_data)
                return True
            except Exception as e:
                print(f"Failed to send mesh: {e}")
                try:
                    self.client_socket.close()
                except:
                    pass
                self.client_socket = None
                return False

    def send_instance_data(self, instance_data: bytes) -> bool:
        """
        Send instance data to connected Unity client

        Args:
            instance_data: Binary instance data (transforms for GPU instancing)

        Returns:
            True if sent successfully, False otherwise
        """
        with self.lock:
            if not self.client_socket:
                return False

            try:
                # Message format: [type:1byte][length:4bytes][payload]
                message_type = 0x02  # Instance data
                length = len(instance_data)
                header = struct.pack('<B I', message_type, length)  # < = little-endian

                self.client_socket.sendall(header + instance_data)
                return True
            except Exception as e:
                print(f"Failed to send instances: {e}")
                try:
                    self.client_socket.close()
                except:
                    pass
                self.client_socket = None
                return False

    def is_connected(self) -> bool:
        """Check if Unity client is connected"""
        with self.lock:
            return self.client_socket is not None


# Global server instance
_server_instance: Optional[MeshStreamServer] = None


def get_server() -> MeshStreamServer:
    """Get or create the global server instance"""
    global _server_instance
    if _server_instance is None:
        _server_instance = MeshStreamServer()
    return _server_instance


def cleanup_server():
    """Clean up the global server instance"""
    global _server_instance
    if _server_instance:
        _server_instance.stop()
        _server_instance = None
