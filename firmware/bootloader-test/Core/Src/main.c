/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2024 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "dma.h"
#include "usart.h"
#include "gpio.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include <string.h>
#include <stdio.h>
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */
#define RX_BUFFER_SIZE 64
uint8_t rx_buffer[RX_BUFFER_SIZE];
volatile uint8_t do_programming_flag = 0;
/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
/* USER CODE BEGIN PFP */
static void CheckForCommand(void);
static void JumpToBootloader(void);
static void read_version(void);
/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */
__attribute__((section(".my_version_section"))) const char version_string[] = "VER 1 0 0";
/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_DMA_Init();
  MX_USART1_UART_Init();
  /* USER CODE BEGIN 2 */

  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */

  // Start DMA reception
  HAL_UART_Receive_DMA(&huart1, rx_buffer, RX_BUFFER_SIZE);
  uint32_t prevTick = 0;

  while (1)
  {
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
	  CheckForCommand();

	  if((HAL_GetTick() - prevTick >= 250)
			  && do_programming_flag == 0)
	  {
		  HAL_GPIO_TogglePin(LD2_GPIO_Port, LD2_Pin);
		  char buf[256];
		  int size;
		  size = sprintf(buf, "Toggle led...\n\r");
		  HAL_UART_Transmit(&huart1, (uint8_t*)buf, size, 5);
		  read_version();
		  prevTick = HAL_GetTick();
	  }

	  if(do_programming_flag == 1)
	  {
		  char buf[256];
		  int size;
		  size = sprintf(buf, "Going into bootloader mode...\n\r");
		  HAL_UART_Transmit(&huart1, (uint8_t*)buf, size, 5);
		  HAL_GPIO_WritePin(LD2_GPIO_Port, LD2_Pin, GPIO_PIN_SET);
		  JumpToBootloader();
	  }
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Configure the main internal regulator output voltage
  */
  __HAL_RCC_PWR_CLK_ENABLE();
  __HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE2);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSI;
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;
  RCC_OscInitStruct.HSICalibrationValue = RCC_HSICALIBRATION_DEFAULT;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSI;
  RCC_OscInitStruct.PLL.PLLM = 16;
  RCC_OscInitStruct.PLL.PLLN = 336;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV4;
  RCC_OscInitStruct.PLL.PLLQ = 7;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV2;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_2) != HAL_OK)
  {
    Error_Handler();
  }
}

/* USER CODE BEGIN 4 */
void CheckForCommand(void)
{
    // Example: Look for "DO_PROGRAMMING" command in the buffer
    if (strstr((char*)rx_buffer, "DO_PROGRAMMING") != NULL) {
        do_programming_flag = 1;
        // Clear the command from the buffer (optional)
        memset(rx_buffer, 0, RX_BUFFER_SIZE);
    }
}

#define VERSION_ADDRESS 0x08005000

void read_version(void) {
    const char *version = (const char *)VERSION_ADDRESS;
    char buf[256];
    int size;
    size = sprintf(buf, "Firmware version: %s\n\r", version);
    HAL_UART_Transmit(&huart1, (uint8_t*)buf, size, 5);
}

void JumpToBootloader(void)
{
	void (*SysMemBootJump)(void);

	    /**
	     * Step: Set system memory address.
	     *
	     *       For STM32F429, system memory is on 0x1FFF 0000
	     *       For other families, check AN2606 document table 110 with descriptions of memory addresses
	     */
	    volatile uint32_t addr = 0x1FFF0000;

	    /**
	     * Step: Disable RCC, set it to default (after reset) settings
	     *       Internal clock, no PLL, etc.
	     */
	    HAL_RCC_DeInit();
	    HAL_DeInit(); // add by ctien
	    /**
	     * Step: Disable systick timer and reset it to default values
	     */
	    SysTick->CTRL = 0;
	    SysTick->LOAD = 0;
	    SysTick->VAL = 0;

	    /**
	     * Step: Disable all interrupts
	     */
	//    __disable_irq(); // changed by ctien

	    /**
	     * Step: Remap system memory to address 0x0000 0000 in address space
	     *       For each family registers may be different.
	     *       Check reference manual for each family.
	     *
	     *       For STM32F4xx, MEMRMP register in SYSCFG is used (bits[1:0])
	     *       For STM32F0xx, CFGR1 register in SYSCFG is used (bits[1:0])
	     *       For others, check family reference manual
	     */
	    SYSCFG->MEMRMP = 0x01;
	    //} ...or if you use HAL drivers
	    //__HAL_SYSCFG_REMAPMEMORY_SYSTEMFLASH();    //Call HAL macro to do this for you

	    /**
	     * Step: Set jump memory location for system memory
	     *       Use address with 4 bytes offset which specifies jump location where program starts
	     */
	    SysMemBootJump = (void (*)(void)) (*((uint32_t *)(addr + 4)));

	    /**
	     * Step: Set main stack pointer.
	     *       This step must be done last otherwise local variables in this function
	     *       don't have proper value since stack pointer is located on different position
	     *
	     *       Set direct address location which specifies stack pointer in SRAM location
	     */
	    __set_MSP(*(uint32_t *)addr);

	    /**
	     * Step: Actually call our function to jump to set location
	     *       This will start system memory execution
	     */
	    SysMemBootJump();

	    /**
	     * Step: Connect USB<->UART converter to dedicated USART pins and test
	     *       and test with bootloader works with STM32 STM32 Cube Programmer
	     */
}
/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}

#ifdef  USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
