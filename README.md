# Flash loader gui example 
This repository contains simple example usage of the flash loading gui application that is communicate with bootloader, stored in the internal boot ROM (system memory) of STM32 devices, and programmed during production. It is using UART interface so can be connected by any ethernet/usb to uart converter.

In this example **there is no need to change any hardware BOOT0/BOOT1 pin connections**, the gui software sends initial bootloader command to microcontroller and it is automatically stops executing program and then goes into bootloader mode. After programming it is jump to user application again.


<img src="https://github.com/user-attachments/assets/d19bc9ef-5cf3-4dcd-bab1-a1ade9672d88">


This example is based on following hardware, but it could be simply modify to use any other STM32 microcontrollers.

- [NUCLEO-F401RE](https://www.st.com/en/evaluation-tools/nucleo-f401re.html)
- [UART to Ethernet converter](https://www.waveshare.com/wiki/2-CH_UART_TO_ETH)
- Any UART to USB converter

For more information please follow:
- [AN2606](https://www.st.com/resource/en/application_note/an2606-stm32-microcontroller-system-memory-boot-mode-stmicroelectronics.pdf)
- [AN3155](https://www.st.com/resource/en/application_note/an3155-usart-protocol-used-in-the-stm32-bootloader-stmicroelectronics.pdf)

## Usage

1. Connect to the board by serial or ethernet connection
2. Select .bin file to flash
3. Click upload

## Example
[![Bootloader](https://github.com/user-attachments/assets/93fbd931-8d53-496b-a1cf-2e712f0f45a9)](https://youtu.be/eKDsHOM3AXc "STM32 Bootloader")



