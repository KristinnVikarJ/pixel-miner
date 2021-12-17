/**
 * CUDA MD5 cracker
 * Copyright (C) 2015  Konrad Kusnierz <iryont@gmail.com>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program; if not, write to the Free Software Foundation, Inc.,
 * 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
 */

#include <stdio.h>
#include <iostream>
#include <time.h>
#include <string.h>
#include <stdlib.h>
#include <stdint.h>
#include <sstream>
#include <csignal>

#include <cuda_runtime.h>
#include <cuda_runtime_api.h>
#include <curand_kernel.h>

#define CONST_WORD_LIMIT 10
#define CONST_CHARSET_LIMIT 100

#define CONST_CHARSET "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
#define CONST_CHARSET_LENGTH (sizeof(CONST_CHARSET) - 1)

#define CONST_WORD_LENGTH_MIN 1
#define CONST_WORD_LENGTH_MAX 8

#define TOTAL_BLOCKS 80UL//16384UL
#define TOTAL_THREADS 32UL
#define HASHES_PER_KERNEL 128UL

#include "assert.cu"
#include "md5.cu"

/* Global variables */
uint8_t g_wordLength;

char g_word[(CONST_WORD_LIMIT)];
char g_charset[CONST_CHARSET_LIMIT];
char g_cracked[1024][10];
uint32_t salts[1024*4];
uint32_t* hashes = new uint32_t[1024];

__device__ char g_deviceCracked[1024][10];
__device__ char g_deviceCharset[CONST_CHARSET_LIMIT];

__device__ __host__ bool next(uint8_t* length, char* word, uint32_t increment){
  uint32_t idx = 0;
  uint32_t add = 0;
  
  while(increment > 0 && idx < (CONST_WORD_LIMIT)){
    if(idx >= *length && increment > 0){
      increment--;
    }
    
    add = increment + word[idx];
    word[idx] = add % CONST_CHARSET_LENGTH;
    increment = add / CONST_CHARSET_LENGTH;
    idx++;
  }
  
  if(idx > *length){
    *length = idx;
  }
  
  if(idx > CONST_WORD_LENGTH_MAX){
    return false;
  }

  return true;
}

void md5Hash_salt(unsigned char* data, uint32_t length, uint32_t *a1, uint32_t *b1, uint32_t *c1, uint32_t *d1){
  const uint32_t a0 = 0x67452301;
  const uint32_t b0 = 0xEFCDAB89;
  const uint32_t c0 = 0x98BADCFE;
  const uint32_t d0 = 0x10325476;

  uint32_t a = 0;
  uint32_t b = 0;
  uint32_t c = 0;
  uint32_t d = 0;

  uint32_t vals[16] = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};

  int i = 0;
  for(i=0; i < length; i++){
    vals[i / 4] |= data[i] << ((i % 4) * 8);
  }

  #define lin0  (vals[0])//x
  #define lin1  (vals[1])//y
  #define lin2  (vals[2])//z
  #define lin3  (vals[3])
  #define lin4  (vals[4])
  #define lin5  (vals[5])
  #define lin6  (vals[6])
  #define lin7  (vals[7])
  #define lin8  (vals[8])
  #define lin9  (vals[9])
  #define lin10 (vals[10])
  #define lin11 (vals[11])
  #define lin12 (vals[12])
  #define lin13 (vals[13])
  #define lin14 (vals[14])
  #define lin15 (vals[15])

  //Initialize hash value for this chunk:
  a = a0;
  b = b0;
  c = c0;
  d = d0;

  /* Round 1 */
  #define S11 7
  #define S12 12
  #define S13 17
  #define S14 22
  FF ( a, b, c, d, lin0,  S11, 3614090360); /* 1 */
  FF ( d, a, b, c, lin1,  S12, 3905402710); /* 2 */
  FF ( c, d, a, b, lin2,  S13,  606105819); /* 3 */
  FF ( b, c, d, a, lin3,  S14, 3250441966); /* 4 */
  FF ( a, b, c, d, lin4,  S11, 4118548399); /* 5 */
  FF ( d, a, b, c, lin5,  S12, 1200080426); /* 6 */
  FF ( c, d, a, b, lin6,  S13, 2821735955); /* 7 */
  FF ( b, c, d, a, lin7,  S14, 4249261313); /* 8 */
  FF ( a, b, c, d, lin8,  S11, 1770035416); /* 9 */
  FF ( d, a, b, c, lin9,  S12, 2336552879); /* 10 */
  FF ( c, d, a, b, lin10, S13, 4294925233); /* 11 */
  FF ( b, c, d, a, lin11, S14, 2304563134); /* 12 */
  FF ( a, b, c, d, lin12, S11, 1804603682); /* 13 */
  FF ( d, a, b, c, lin13, S12, 4254626195); /* 14 */
  FF ( c, d, a, b, lin14, S13, 2792965006); /* 15 */
  FF ( b, c, d, a, lin15, S14, 1236535329); /* 16 */

  /* Round 2 */
  #define S21 5
  #define S22 9
  #define S23 14
  #define S24 20
  GG ( a, b, c, d, lin1, S21, 4129170786); /* 17 */
  GG ( d, a, b, c, lin6, S22, 3225465664); /* 18 */
  GG ( c, d, a, b, lin11, S23,  643717713); /* 19 */
  GG ( b, c, d, a, lin0, S24, 3921069994); /* 20 */
  GG ( a, b, c, d, lin5, S21, 3593408605); /* 21 */
  GG ( d, a, b, c, lin10, S22,   38016083); /* 22 */
  GG ( c, d, a, b, lin15, S23, 3634488961); /* 23 */
  GG ( b, c, d, a, lin4, S24, 3889429448); /* 24 */
  GG ( a, b, c, d, lin9, S21,  568446438); /* 25 */
  GG ( d, a, b, c, lin14, S22, 3275163606); /* 26 */
  GG ( c, d, a, b, lin3, S23, 4107603335); /* 27 */
  GG ( b, c, d, a, lin8, S24, 1163531501); /* 28 */
  GG ( a, b, c, d, lin13, S21, 2850285829); /* 29 */
  GG ( d, a, b, c, lin2, S22, 4243563512); /* 30 */
  GG ( c, d, a, b, lin7, S23, 1735328473); /* 31 */
  GG ( b, c, d, a, lin12, S24, 2368359562); /* 32 */

  /* Round 3 */
  #define S31 4
  #define S32 11
  #define S33 16
  #define S34 23
  HH ( a, b, c, d, lin5, S31, 4294588738); /* 33 */
  HH ( d, a, b, c, lin8, S32, 2272392833); /* 34 */
  HH ( c, d, a, b, lin11, S33, 1839030562); /* 35 */
  HH ( b, c, d, a, lin14, S34, 4259657740); /* 36 */
  HH ( a, b, c, d, lin1, S31, 2763975236); /* 37 */
  HH ( d, a, b, c, lin4, S32, 1272893353); /* 38 */
  HH ( c, d, a, b, lin7, S33, 4139469664); /* 39 */
  HH ( b, c, d, a, lin10, S34, 3200236656); /* 40 */
  HH ( a, b, c, d, lin13, S31,  681279174); /* 41 */
  HH ( d, a, b, c, lin0, S32, 3936430074); /* 42 */
  HH ( c, d, a, b, lin3, S33, 3572445317); /* 43 */
  HH ( b, c, d, a, lin6, S34,   76029189); /* 44 */
  HH ( a, b, c, d, lin9, S31, 3654602809); /* 45 */
  HH ( d, a, b, c, lin12, S32, 3873151461); /* 46 */
  HH ( c, d, a, b, lin15, S33,  530742520); /* 47 */
  HH ( b, c, d, a, lin2, S34, 3299628645); /* 48 */

  /* Round 4 */
  #define S41 6
  #define S42 10
  #define S43 15
  #define S44 21
  II ( a, b, c, d, lin0, S41, 4096336452); /* 49 */
  II ( d, a, b, c, lin7, S42, 1126891415); /* 50 */
  II ( c, d, a, b, lin14, S43, 2878612391); /* 51 */
  II ( b, c, d, a, lin5, S44, 4237533241); /* 52 */
  II ( a, b, c, d, lin12, S41, 1700485571); /* 53 */
  II ( d, a, b, c, lin3, S42, 2399980690); /* 54 */
  II ( c, d, a, b, lin10, S43, 4293915773); /* 55 */
  II ( b, c, d, a, lin1, S44, 2240044497); /* 56 */
  II ( a, b, c, d, lin8, S41, 1873313359); /* 57 */
  II ( d, a, b, c, lin15, S42, 4264355552); /* 58 */
  II ( c, d, a, b, lin6, S43, 2734768916); /* 59 */
  II ( b, c, d, a, lin13, S44, 1309151649); /* 60 */
  II ( a, b, c, d, lin4, S41, 4149444226); /* 61 */
  II ( d, a, b, c, lin11, S42, 3174756917); /* 62 */
  II ( c, d, a, b, lin2, S43,  718787259); /* 63 */
  II ( b, c, d, a, lin9, S44, 3951481745); /* 64 */

  *a1 = a+a0;
  *b1 = b+b0;
  *c1 = c+c0;
  *d1 = d+d0;
}

__global__ void md5Crack(uint8_t wordLength, char* charsetWord, uint32_t hash01[], uint32_t salts[1024*4], uint32_t offset){
  uint32_t idx = (blockIdx.x * blockDim.x + threadIdx.x) * HASHES_PER_KERNEL;
  uint32_t index = (blockIdx.x/40);
  /* Shared variables */
  __shared__ char sharedCharset[CONST_CHARSET_LIMIT];
  
  /* Thread variables */
  char threadCharsetWord[CONST_WORD_LIMIT];
  char threadTextWord[CONST_WORD_LIMIT];
  uint8_t threadWordLength;
  uint32_t threadHash01, threadHash02, threadHash03, threadHash04;

  /* Copy everything to local memory */
  memcpy(threadCharsetWord, charsetWord, CONST_WORD_LIMIT);
  memcpy(&threadWordLength, &wordLength, sizeof(uint8_t));
  memcpy(sharedCharset, g_deviceCharset, sizeof(uint8_t) * CONST_CHARSET_LIMIT);
  /* Increment current word by thread index */
  next(&threadWordLength, threadCharsetWord, idx);
  for(uint32_t hash = 0; hash < HASHES_PER_KERNEL; hash++){
    for(uint32_t i = 0; i < threadWordLength; i++){
      threadTextWord[i] = sharedCharset[threadCharsetWord[i]];
    }
    md5Hash((unsigned char*)threadTextWord, threadWordLength, &threadHash01, &threadHash02, &threadHash03, &threadHash04, salts[((index*4)+(offset*64))+0], salts[((index*4)+(offset*64))+1], salts[((index*4)+(offset*64))+2], salts[((index*4)+(offset*64))+3]);
    //printf("probably illegal\n");
    if((threadHash01 & 0xFFFFFF) == hash01[index+(offset*64)]){
      memcpy(g_deviceCracked[index+(offset*64)], threadTextWord, threadWordLength);
      break;
    }
    
    if(!next(&threadWordLength, threadCharsetWord, 1)){
      break;
    }
  }
}

int main(int argc, char* argv[]){
  /* Check arguments */
  if(argc != 2){
    std::cout << argv[0] << " <infile>" << std::endl;
    return -1;
  }
  
  /* Time */
  cudaEvent_t clockBegin;
  cudaEvent_t clockLast;
  
  cudaEventCreate(&clockBegin);
  cudaEventCreate(&clockLast);
  cudaEventRecord(clockBegin, 0);

  FILE* infile = fopen(argv[1], "r");
  if(infile == NULL){
    std::cout << "Could not open file " << argv[1] << std::endl;
    return -1;
  }
  char** original = (char**)malloc(sizeof(char) * 1024*6);
  //read hashes and salt from infile
  for(int i = 0; i < 1024; i++){
    char* salt = new char[64];
    char* hash = new char[6];
    fscanf(infile, "%s %s", salt, hash);
    uint32_t hash_inp = strtol(hash, NULL, 16);  
    uint32_t Hash = ((hash_inp>>24)&0xff) | // move byte 3 to byte 0
                      ((hash_inp<<8)&0xff0000) | // move byte 1 to byte 2
                      ((hash_inp>>8)&0xff00) | // move byte 2 to byte 1
                      ((hash_inp<<24)&0xff000000); // byte 0 to byte 3
    Hash >>= 8;
    hashes[i] = Hash;
    original[i] = hash;
    md5Hash_salt((unsigned char*)salt, 64, &salts[(i*4)+0], &salts[(i*4)+1], &salts[(i*4)+2], &salts[(i*4)+3]);
  }
  // close handle to file
  fclose(infile);
  
  /* Amount of available devices */
  int devices;
  ERROR_CHECK(cudaGetDeviceCount(&devices));
  
  /* Sync type */
  ERROR_CHECK(cudaSetDeviceFlags(cudaDeviceScheduleSpin));
  
  /* Fill memory */
  memset(g_word, 0, CONST_WORD_LIMIT);
  memcpy(g_charset, CONST_CHARSET, CONST_CHARSET_LENGTH);
  
  /* Current word length = minimum word length */
  g_wordLength = CONST_WORD_LENGTH_MIN;
  
  /* Main device */
  cudaSetDevice(0);

  uint32_t* DeviceHashArr;
  uint32_t* DeviceSaltArr;
  
  /* Current word is different on each device */
  char** words = new char*[devices];

  for(int device = 0; device < devices; device++){
    cudaSetDevice(device);
    
    /* Copy to each device */
    ERROR_CHECK(cudaMemcpyToSymbol(g_deviceCharset, g_charset, sizeof(uint8_t) * CONST_CHARSET_LIMIT, 0, cudaMemcpyHostToDevice));
    ERROR_CHECK(cudaMemcpyToSymbol(g_deviceCracked, g_cracked, sizeof(char) * 1024 * 10, 0, cudaMemcpyHostToDevice));
    
    /* Allocate on each device */
    ERROR_CHECK(cudaMalloc((void**)&words[device], sizeof(uint8_t) * (CONST_WORD_LIMIT)));
    ERROR_CHECK(cudaMalloc((void**)&DeviceHashArr, sizeof(uint32_t) * (1024)));
    ERROR_CHECK(cudaMalloc((void**)&DeviceSaltArr, sizeof(uint32_t) * (1024*4)));
  }

  char** foundWords = new char*[1024];
  int currentIndex = 0;
  while(true){    
    for(int device = 0; device < devices; device++){
      cudaSetDevice(device);
      
      /* Copy current data */
      ERROR_CHECK(cudaMemcpy(words[device], g_word, sizeof(uint8_t) * (CONST_WORD_LIMIT), cudaMemcpyHostToDevice)); 
      ERROR_CHECK(cudaMemcpy(DeviceHashArr, hashes, sizeof(uint32_t) * (1024), cudaMemcpyHostToDevice)); 
      ERROR_CHECK(cudaMemcpy(DeviceSaltArr, salts, sizeof(uint32_t) * (4096), cudaMemcpyHostToDevice));
    
      /* Start kernel */
      md5Crack<<<TOTAL_BLOCKS*64, TOTAL_THREADS>>>(g_wordLength, words[device], DeviceHashArr, DeviceSaltArr, currentIndex);
      cudaError_t error = cudaGetLastError();
      if(error){
        std::cout << "Error: " << cudaGetErrorString(error) << std::endl;
        return -1;
      }
      
      /* Global increment */
      next(&g_wordLength, g_word, TOTAL_THREADS * HASHES_PER_KERNEL * TOTAL_BLOCKS);
    }
          
    /* Synchronize now */
    cudaDeviceSynchronize();
    
    /* Copy result */
    ERROR_CHECK(cudaMemcpyFromSymbol(g_cracked, g_deviceCracked, sizeof(char) * 1024 * 10, 0, cudaMemcpyDeviceToHost)); 
    
    /* Check result */
    int found = 0;
    for(int i = 0; i < 64; i++){
      if(g_cracked[i+(currentIndex*64)][0] != 0){
        found += 1;
      }
    }
    if(found == 64){
      for(int i = 0; i < 64; i++){
        foundWords[i+(currentIndex*64)] = g_cracked[i+(currentIndex*64)];
      }
      currentIndex += 1;
    }
    if (currentIndex == (1024/64)){
      break;
    }
  }

  FILE* outfile = fopen("outfile.txt", "w+");
  for(int i = 0; i < 1024; i++){
    if(foundWords[i] != NULL){
      fprintf(outfile, "%s %s\n", original[i], foundWords[i]);
    }
  }
  fclose(outfile);
  
  for(int device = 0; device < devices; device++){
    cudaSetDevice(device);
    
    /* Free on each device */
    cudaFree((void**)words[device]);
  }
  
  /* Free array */
  delete[] words;
  
  /* Main device */
  cudaSetDevice(0);

  
  float milliseconds = 0;
  
  cudaEventRecord(clockLast, 0);
  cudaEventSynchronize(clockLast);
  cudaEventElapsedTime(&milliseconds, clockBegin, clockLast);
  
  std::cout << "Notice: computation time " << milliseconds << " ms" << std::endl;
  
  cudaEventDestroy(clockBegin);
  cudaEventDestroy(clockLast);
}
