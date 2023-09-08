# Save Chat
A simple chatting application made in C# with the purpose of create a secure and encrypted communication between two or more clients. 
<br><br>
Application developed as a project for **Tópicos de Segurança** in **Curso Técnico Profissional de Programação de Sistemas de Informação**.
![Logo IPL](https://www.ipleiria.pt/wp-content/uploads/2022/04/estg_h.svg)

## Architecture
1. The server starts
2. The server opens a port
3. The server creates a symmetrical key that will be used for the communication between clients 
4. The server creates a public and private key that will be used to send clients their credentials safely
5. The client starts
6. The client creates their own public and private key
7. The client joins the server (without being logged in)
8. The client and server trade their public keys
9. The client uses the server's public key to encrypt their credentials and sends them to the server
10. The server receives the encrypted credentials and uses their private key to decrypt them
11. The server authenticates the credentials and sends a confirmation to the client
12. In case the credentials are valid, the server encrypts the symmetrical key with the public key from the client, signs it with his private key and sends it to the client
13. The client receives the encrypted symmetrical key from the server and decrypts it with his private key
14. The client from now on can encrypt/decrypt all the messages with the symmetrical key

## Credentials
| Username  | Password |
|-----------|----------|
| marco     | 123      |
| tomas     | 123      |
| guilherme | 123      |
| pedro     | 123      |
| bernardo  | 123      |
| fernardo  | 123      |
| luis      | 123      |

## Application developed by:
- Marco Padeiro  nº2211868
- Tomas Moura  nº2211866
- Guilherme Silvestre  nº2211863

